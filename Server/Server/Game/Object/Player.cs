using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.DB;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game
{
    public class Player : GameObject
    {
        public int PlayerDbId { get; set; }
        public ClientSession Session { get; set; }

        public Inventory Inven { get; private set; } = new Inventory();

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
    }
}
