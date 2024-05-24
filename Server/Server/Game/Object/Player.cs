using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.DB;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Server.Game
{
    public class Player : GameObject
    {
        public int PlayerDbId { get; set; }
        public ClientSession Session { get; set; }

        public Inventory Inven { get; private set; } = new Inventory();

        public int WeaponDamage { get; private set; }
        public int ArmorDefence { get; private set; }

        public override int TotalAttack { get { return Stat.Attack + WeaponDamage; } }
        public override int TotalDefence { get { return ArmorDefence; } }

        public Player()
        {
            ObjectType = GameObjectType.Player;
        }

        public override void OnDamaged(GameObject attacker, int damage)
        {
            base.OnDamaged(attacker, damage);
        }

        public override void OnDead(GameObject attacker)
        {
            base.OnDead(attacker);
        }

        public void OnLeaveGame()
        {
            // TODO
            // DB 연동
            // -- 피가 깎일때마다 DB 접근할 필요가 있을까?
            // 1) 이렇게 할 경우 서버가 다운되면 아직 저장되지 않는 정보가 날라간다.
            // 2) 코드 흐름을 다 막아버린다!! (thread 하나가 DB접근해서 처리가 늦어지면 나머지가 다 느려질 수 있다, Room의 jobThread가 느려질 수 있다)
            // -- 비동기(Async) 방법 사용?
            // -- 다른Thread로 DB 일감을 던지면 되지 않을까?
            // -- 결과를 받아서 이어서 처리를 해야 하는 경우가 많음.(아이템 생성, 강화등등?? 메모리에서만 생성하고 처리하면 안됨, DB추가 후 결과를 받아 처리해야하는 경우)

            //그래서 결론은 다른 Db 작업용 Thread에 일감을 던져서 처리하도록 한다.
            DbTransaction.SavePlayerStatus_Step1(this, Room);
        }

        public void HandleEquipItem(C_EquipItem equipPacket)
        {
            Item item = Inven.Get(equipPacket.ItemDbId);
            if (item == null)
                return;

            if (item.ItemType == ItemType.Consumable)
                return;

            // 착용 요청이라면 겹치는 부위는 해제
            if (equipPacket.Equipped)
            {
                Item unEquipItem = null;
                if (item.ItemType == ItemType.Weapon)
                {
                    unEquipItem = Inven.Find(i => i.Equipped && i.ItemType == ItemType.Weapon);

                }
                else if (item.ItemType == ItemType.Armor)
                {
                    ArmorType armorType = ((Armor)item).ArmorType;
                    unEquipItem = Inven.Find(i => i.Equipped && i.ItemType == ItemType.Armor && ((Armor)i).ArmorType == armorType);
                }

                if (unEquipItem != null)
                {
                    // 메모리 선 적용 (중요하지 않고 메모리에서 선적용해도 문제 없을 경우)
                    unEquipItem.Equipped = false;

                    // DB에 noti
                    DbTransaction.EquipItemNoti(this, unEquipItem);

                    // 클라에 통보
                    S_EquipItem equipOkItem = new S_EquipItem();
                    equipOkItem.ItemDbId = unEquipItem.ItemDbId;
                    equipOkItem.Equipped = unEquipItem.Equipped;
                    this.Session.Send(equipOkItem);
                }
            }

            {
                // 메모리 선 적용 (중요하지 않고 메모리에서 선적용해도 문제 없을 경우)
                item.Equipped = equipPacket.Equipped;

                // DB에 noti
                DbTransaction.EquipItemNoti(this, item);

                // 클라에 통보
                S_EquipItem equipOkItem = new S_EquipItem();
                equipOkItem.ItemDbId = equipPacket.ItemDbId;
                equipOkItem.Equipped = equipPacket.Equipped;
                this.Session.Send(equipOkItem);
            }

            // 추가 스탯 적용
            RefreshAdditionalStat();
        }

        public void RefreshAdditionalStat()
        {
            WeaponDamage = 0;
            ArmorDefence = 0;

            foreach(Item item in Inven.Items.Values)
            {
                if(item.Equipped == false)
                    continue;

                switch(item.ItemType)
                {
                    case ItemType.Weapon:
                        WeaponDamage += ((Weapon)item).Damage;
                        break;
                    case ItemType.Armor:
                        ArmorDefence += ((Armor)item).Defence;
                        break;
                }
            }
        }
    }
}
