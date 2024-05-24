using Google.Protobuf.Protocol;
using Server.Data;
using Server.DB;
using System;
using System.Collections.Generic;

namespace Server.Game
{
    public class Monster : GameObject
    {
        public int TemplateId { get; private set; }
        public Monster()
        {
            ObjectType = GameObjectType.Monster;
        }

        public void Init(int templateId)
        {
            TemplateId = templateId;

            MonsterData monsterData = null;
            DataManager.MonsterDict.TryGetValue(templateId, out monsterData);
            Stat.MergeFrom(monsterData.stat);
            Stat.Hp = monsterData.stat.MaxHp;

            State = CreatureState.Idle;
        }

        // FSM (Finite State Machine)
        public override void Update()
        {
            switch(State)
            {
                case CreatureState.Idle:
                    UpdateIdle();
                    break;
                case CreatureState.Moving:
                    UpdateMoving();
                    break;
                case CreatureState.Skill:
                    UpdateSkill();
                    break;
                case CreatureState.Dead:
                    UpdateDead();
                    break;
            }
        }

        Player _target;
        int _searchCellDist = 10;
        int _chaseCellDist = 20;
        long _nextSearchTick = 0;
        protected virtual void UpdateIdle()
        {
            if (_nextSearchTick > Environment.TickCount64)
                return;

            _nextSearchTick = Environment.TickCount64 + 1000;

            Player target = Room.FindPlayer(p =>
            {
                Vector2Int dir = p.CellPos - CellPos;
                return dir.cellDistFromZero <= _searchCellDist;
            });

            if (target == null)
                return;

            _target = target;
            State = CreatureState.Moving;
        }

        int _skillRange = 1;
        long _nextMovingTick = 0;
        protected virtual void UpdateMoving()
        {
            if (_nextMovingTick > Environment.TickCount64)
                return;

            int moveTick = (int)(1000 / Speed);
            _nextMovingTick = Environment.TickCount64 + moveTick;

            if(_target == null || _target.Room != Room)
            {
                _target = null;
                State = CreatureState.Idle;
                BroadcastMove();
                return;
            }

            Vector2Int dir = _target.CellPos - CellPos;
            int dist = dir.cellDistFromZero;
            if(dist == 0 || dist > _chaseCellDist)
            {
                _target = null;
                State = CreatureState.Idle;
                BroadcastMove();
                return;
            }

            List<Vector2Int> path = Room.Map.FindPath(CellPos, _target.CellPos, checkObjects: false);
            if(path.Count < 2 || path.Count > _chaseCellDist)
            {
                _target = null;
                State = CreatureState.Idle;
                BroadcastMove();
                return;
            }

            // 스킬로 넘어갈지 체크
            if(dist <= _skillRange && (dir.x == 0 || dir.y == 0))
            {
                _coolTick = 0;
                State = CreatureState.Skill;
                return;
            }

            // 이동
            Dir = GetDirFromVec(path[1] - CellPos);
            Room.Map.ApplyMove(this, path[1]);
            BroadcastMove();
        }

        long _coolTick = 0;
        protected virtual void UpdateSkill()
        {
            if(_coolTick == 0)
            {
                // 유효한 타겟인지 체크
                if(_target == null || _target.Room != Room || _target.Hp == 0)
                {
                    _target = null;
                    State = CreatureState.Moving;
                    BroadcastMove();
                    return;
                }

                // 스킬이 아직 사용 가능한지
                Vector2Int dir = (_target.CellPos - CellPos);
                int dist = dir.cellDistFromZero;
                bool canUseSkill = (dist <= _skillRange && (dir.x == 0 || dir.y == 0));
                if(canUseSkill == false)
                {
                    State = CreatureState.Moving;
                    BroadcastMove();
                    return;
                }

                // 타겟팅 방향 주시
                MoveDir lookDir = GetDirFromVec(dir);
                if(Dir != lookDir)
                {
                    Dir = lookDir;
                    BroadcastMove();
                }

                Skill SkillData = null;
                DataManager.SkillDict.TryGetValue(1, out SkillData);

                // 데미지 판정
                _target.OnDamaged(this, SkillData.damage + TotalAttack);

                // 스킬 사용했다고 알리기
                S_Skill skillPacket = new S_Skill() { Info = new SkillInfo() };
                skillPacket.ObjectId = Id;
                skillPacket.Info.SkillId = SkillData.id;
                Room.Broadcast(skillPacket);

                // 스킬 쿨타임 적용
                int coolTick = (int)(1000 * SkillData.cooldown);
                _coolTick = Environment.TickCount64 + coolTick;
            }

            if (_coolTick > Environment.TickCount64)
                return;

            _coolTick = 0;
        }

        protected virtual void UpdateDead()
        {

        }

        void BroadcastMove()
        {
            // 다른 플레이어게 알려준다
            S_Move movePacket = new S_Move();
            movePacket.ObjectId = Id;
            movePacket.PosInfo = PosInfo;
            Room.Broadcast(movePacket);
        }

        public override void OnDead(GameObject attacker)
        {
            base.OnDead(attacker);

            GameObject owner = attacker.GetOwner();

            if(owner.ObjectType == GameObjectType.Player)
            {
                RewardData rewardData = GetRandomReward();
                if(rewardData != null)
                {
                    Player player = (Player)owner;
                    DbTransaction.RewardPlayer(player, rewardData, Room);
                }
            }
        }

        RewardData GetRandomReward()
        {
            MonsterData monsterData = null;
            DataManager.MonsterDict.TryGetValue(TemplateId, out monsterData);

            // rand = 0 ~ 100 -> 42
            // 10 10 10 10 10
            int sum = 0;
            int rand = new Random().Next(1, 101);
            foreach(RewardData rewardData in monsterData.rewards)
            {
                sum += rewardData.probability;
                if(rand <= sum)
                    return rewardData;
            }

            return null;
        }
    }
}
