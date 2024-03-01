using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Define;

public class ArrowController : CreatureController
{
    protected override void Init()
    {
        base.Init();
    }

    protected override void UpdateAnimation()
    {

    }

    // 이동 가능한 상태일 때, 실제 좌표를 이동한다.
    protected override void UpdateIdle()
    {
        if (_dir != MoveDir.None)
        {
            Vector3Int destPos = CellPos;

            switch (_dir)
            {
                case MoveDir.Up:
                    destPos += Vector3Int.up;
                    break;
                case MoveDir.Down:
                    destPos += Vector3Int.down;
                    break;
                case MoveDir.Left:
                    destPos += Vector3Int.left;
                    break;
                case MoveDir.Right:
                    destPos += Vector3Int.right;
                    break;
            }

            State = CreatureState.Moving;

            if (Managers.Map.CanGo(destPos))
            {
                GameObject go = Managers.Object.Find(destPos);
                if (go == null)
                {
                    CellPos = destPos;
                }
                else
                {
                    Debug.Log(go.name);
                    Managers.Resource.Destroy(gameObject);
                }
            }
            else
            {
                Managers.Resource.Destroy(gameObject);
            }
        }
    }
}
