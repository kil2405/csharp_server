# C# 게임 서버 프로젝트 - 전체 구조 분석 보고서

## 목차
1. [프로젝트 구조](#1-프로젝트-구조)
2. [ServerCore - 네트워크 아키텍처](#2-servercore---네트워크-아키텍처)
3. [Server - 게임 로직 계층](#3-server---게임-로직-계층)
4. [패킷 시스템](#4-패킷-시스템)
5. [데이터베이스 연동](#5-데이터베이스-연동)
6. [게임 오브젝트 시스템](#6-게임-오브젝트-시스템)
7. [전체 실행 흐름](#7-전체-실행-흐름)
8. [스레드 안전성](#8-스레드-안전성)
9. [성능 최적화 기법](#9-성능-최적화-기법)

---

## 1. 프로젝트 구조

### 1.1 전체 구성 (4개 프로젝트)

```
Server (솔루션 루트)
├── ServerCore/          (네트워크 기반 라이브러리)
├── Server/              (게임 서버 메인 로직)
├── DummyClient/         (테스트용 더미 클라이언트)
└── PacketGenerator/     (패킷 생성 도구)
```

### 1.2 각 프로젝트 역할

| 프로젝트 | 역할 | 주요 클래스 |
|----------|------|-------------|
| **ServerCore** | 저수준 네트워크 통신 | Session, Listener, Connector, RecvBuffer |
| **Server** | 게임 비즈니스 로직 | GameRoom, Player, Monster, Map |
| **DummyClient** | 부하 테스트/개발 테스트 | SessionManager, ServerSession |
| **PacketGenerator** | 패킷 코드 자동 생성 | - |

### 1.3 의존성 관계

```
DummyClient ──→ ServerCore
     ↑
     │
Server ────────→ ServerCore
```

---

## 2. ServerCore - 네트워크 아키텍처

### 2.1 Session 클래스 (비동기 소켓 관리)

**파일:** `ServerCore/Session.cs`

```csharp
public abstract class Session
{
    Socket _socket;                          // TCP 소켓
    RecvBuffer _recvBuffer;                  // 수신 버퍼
    Queue<ArraySegment<byte>> _sendQueue;    // 송신 큐
    List<ArraySegment<byte>> _pendingList;   // 대기 중인 송신 버퍼
    SocketAsyncEventArgs _sendArgs;          // 비동기 송신 이벤트
    SocketAsyncEventArgs _recvArgs;          // 비동기 수신 이벤트
}

public abstract class PacketSession : Session
{
    // 패킷 헤더: [size(2)][packetId(2)][data...]
    public static readonly int HeaderSize = 2;
}
```

**비동기 네트워크 흐름:**

```
수신 흐름:
ReceiveAsync() → OnRecvCompleted() → RecvBuffer.OnWrite() → OnRecv() → 패킷 처리

송신 흐름:
Send() → _sendQueue에 추가 → SendAsync() → OnSendCompleted() → 다음 송신
```

### 2.2 RecvBuffer (수신 버퍼 관리)

**파일:** `ServerCore/RecvBuffer.cs`

```csharp
public class RecvBuffer
{
    ArraySegment<byte> _buffer;  // 65535 바이트
    int _readPos;                // 읽기 커서
    int _writePos;               // 쓰기 커서
}
```

**버퍼 구조:**
```
[이미 읽음] [아직 안 읽은 데이터] [빈 공간]
^readPos   ^writePos             ^buffer.Count
```

### 2.3 Listener (서버 포트 수신)

**파일:** `ServerCore/Listener.cs`

```csharp
public void Init(IPEndPoint endPoint, Func<Session> sessionFactory)
{
    _listenSocket.Bind(endPoint);     // IP:7777 바인드
    _listenSocket.Listen(100);        // 최대 대기 100개

    for (int i = 0; i < 10; i++)
        RegisterAccept(args);         // 10개의 AcceptAsync 미리 등록
}
```

### 2.4 Connector (클라이언트 연결)

**파일:** `ServerCore/Connector.cs`

DummyClient가 서버에 연결할 때 사용

---

## 3. Server - 게임 로직 계층

### 3.1 멀티스레드 아키텍처

**파일:** `Server/Program.cs`

```
┌─────────────────────────────────────────────────────────────┐
│                    스레드 배치                              │
├─────────────────────────────────────────────────────────────┤
│ MainThread (GameLogic)  ← GameLogic.Update() 무한 루프     │
│ Recv N개 (자동)         ← ServerCore에서 관리              │
│ Send 1개 (NetworkTask)  ← ClientSession.FlushSend()        │
│ DB 1개 (DbTask)         ← DbTransaction.Flush()            │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 JobSerializer (작업 스케줄링)

**파일:** `Server/Game/Job/JobSerializer.cs`

**핵심 개념:** 스레드 안전한 작업 큐

```csharp
public class JobSerializer
{
    JobTimer _timer;              // 지연 작업 관리
    Queue<IJob> _jobQueue;        // 즉시 작업 큐

    public void Push(Action action)              // 즉시 작업 추가
    public IJob PushAfter(int ms, Action action) // 지연 작업 추가
    public void Flush()                          // 모든 작업 실행
}
```

**사용 예시:**
```csharp
// 즉시 실행
room.Push(room.HandleMove, player, movePacket);

// 200ms 후 실행 (몬스터 AI)
monster.Room.PushAfter(200, monster.Update);

// 5초 후 실행 (Ping)
GameLogic.Instance.PushAfter(5000, Ping);
```

### 3.3 GameRoom (게임 방)

**파일:** `Server/Game/Room/GameRoom.cs`

```csharp
public partial class GameRoom : JobSerializer
{
    public int RoomId { get; set; }

    Dictionary<int, Player> _players;
    Dictionary<int, Monster> _monsters;
    Dictionary<int, Projectile> _projectiles;

    public Map Map { get; private set; }
    public Zone[,] Zones { get; private set; }  // 공간 분할 (10x10칸)
}
```

**Zone 시스템 (공간 분할):**
```
Map (1000x1000)
├── Zone[0,0] ─ Players[], Monsters[], Projectiles[]
├── Zone[0,1] ─ Players[], Monsters[], Projectiles[]
├── Zone[1,0] ─ ...
└── ...

각 Zone = 10×10 셀
→ Broadcast 시 인접 Zone만 확인 (최대 9개)
→ 성능 최적화
```

### 3.4 GameObject 계층

**파일:** `Server/Game/Object/`

```
GameObject (기본)
├── Player
│   ├── Session (네트워크)
│   ├── Inventory (아이템)
│   └── VisionCube (시야)
├── Monster
│   └── FSM (Idle/Moving/Skill/Dead)
└── Projectile
    └── Arrow
```

**ID 생성 방식:**
```
[UNUSED(1)][TYPE(7)][ID(24)]

Player:  0x01000000 | counter
Monster: 0x02000000 | counter
Arrow:   0x03000000 | counter
```

### 3.5 Monster AI (FSM)

**파일:** `Server/Game/Object/Monster.cs`

```csharp
public override void Update()
{
    switch(State)
    {
        case CreatureState.Idle:
            UpdateIdle();      // 플레이어 탐색 (1초마다)
            break;
        case CreatureState.Moving:
            UpdateMoving();    // A* 경로 추적
            break;
        case CreatureState.Skill:
            UpdateSkill();     // 공격
            break;
        case CreatureState.Dead:
            UpdateDead();      // 사망 처리
            break;
    }

    // 200ms 후 다시 호출
    _job = Room.PushAfter(200, Update);
}
```

### 3.6 VisionCube (시야 관리)

**파일:** `Server/Game/Room/VisionCube.cs`

```csharp
public void Update()  // 100ms마다 실행
{
    HashSet<GameObject> currentObjects = GatherObjects();  // 5칸 범위

    // 새로 보이는 객체 → S_Spawn 전송
    List<GameObject> added = currentObjects.Except(PreviousObject);

    // 더 이상 안 보이는 객체 → S_Despawn 전송
    List<GameObject> removed = PreviousObject.Except(currentObjects);

    PreviousObject = currentObjects;
}
```

---

## 4. 패킷 시스템

### 4.1 패킷 포맷

```
[Size(2byte)][MsgId(2byte)][Data(protobuf)]

예: C_Move 패킷
[0x00, 0x10]     // Size = 16 bytes
[0x00, 0x01]     // MsgId = 1 (C_Move)
[... protobuf ...] // Protobuf 직렬화 데이터
```

### 4.2 패킷 종류

| 방향 | 패킷 | 설명 |
|------|------|------|
| C→S | C_Login | 로그인 요청 |
| C→S | C_Move | 이동 요청 |
| C→S | C_Skill | 스킬 사용 |
| C→S | C_EquipItem | 아이템 장착 |
| S→C | S_Connected | 연결 완료 |
| S→C | S_EnterGame | 게임 진입 |
| S→C | S_Spawn | 객체 생성 |
| S→C | S_Despawn | 객체 제거 |
| S→C | S_Move | 이동 동기화 |

### 4.3 패킷 처리 흐름

```
클라이언트 → C_Move 전송
       ↓
PacketManager.OnRecvPacket()   // 패킷 ID로 라우팅
       ↓
MakePacket<C_Move>()           // Protobuf 역직렬화
       ↓
PacketHandler.C_MoveHandler()  // 비즈니스 로직
       ↓
room.Push(room.HandleMove)     // GameRoom 작업 큐에 추가
       ↓
GameRoom.HandleMove()          // 실제 처리 (GameLogic 스레드)
       ↓
Broadcast(S_Move)              // 주변 플레이어에게 전송
```

### 4.4 ClientSession (패킷 배치 송신)

**파일:** `Server/Session/ClientSession.cs`

```csharp
public void Send(IMessage packet)
{
    lock (_lock)
    {
        _reserveQueue.Add(sendBuffer);  // 예약만 함
        _reservedSendBytes += sendBuffer.Length;
    }
}

public void FlushSend()  // NetworkTask에서 호출
{
    // 100ms 경과 또는 10KB 초과 시 실제 송신
    if(delta < 100 && _reservedSendBytes < 10000)
        return;

    Send(_reserveQueue);  // 일괄 송신
}
```

---

## 5. 데이터베이스 연동

### 5.1 Entity Framework Core + SQLite

**파일:** `Server/DB/AppDbContext.cs`

```csharp
public class AppDbContext : DbContext
{
    public DbSet<AccountDb> Accounts { get; set; }
    public DbSet<PlayerDb> Players { get; set; }
    public DbSet<ItemDb> Items { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite("Data Source=GameDB.db");
    }
}
```

### 5.2 데이터 모델

```
Account (1) ──── (N) Player (1) ──── (N) Item
```

```csharp
[Table("Account")]
public class AccountDb
{
    public int AccountDbId { get; set; }
    public string AccountName { get; set; }
    public ICollection<PlayerDb> Players { get; set; }
}

[Table("Player")]
public class PlayerDb
{
    public int PlayerDbId { get; set; }
    public string PlayerName { get; set; }
    public int Level, Hp, MaxHp, Attack;
    public float Speed;
    public int TotalExp;
}

[Table("Item")]
public class ItemDb
{
    public int ItemDbId { get; set; }
    public int TemplateId { get; set; }
    public int Count { get; set; }
    public bool Equipped { get; set; }
}
```

### 5.3 DbTransaction (비동기 DB 처리)

**파일:** `Server/DB/DbTransaction.cs`

**핵심:** GameRoom 스레드 ↔ DB 스레드 분리

```csharp
public static void SavePlayerStatus(Player player, GameRoom room)
{
    // 1. GameRoom 스레드에서 데이터 준비
    PlayerDb playerDb = new PlayerDb { Hp = player.Stat.Hp };

    // 2. DB 스레드로 전달
    Instance.Push(() =>
    {
        using (AppDbContext db = new AppDbContext())
        {
            db.SaveChanges();

            // 3. 완료 후 GameRoom으로 콜백
            room.Push(() => { /* 결과 처리 */ });
        }
    });
}
```

---

## 6. 게임 오브젝트 시스템

### 6.1 Item 계층

```
Item (기본)
├── Weapon     (무기 데미지 보너스)
├── Armor      (방어력 보너스)
│   └── ArmorType: 머리/상체/다리/발
└── Consumable (소비 아이템)
```

### 6.2 Inventory

**파일:** `Server/Game/Item/Inventory.cs`

```csharp
public class Inventory
{
    Dictionary<int, Item> _items;  // ItemDbId → Item

    public Item Get(int itemDbId);
    public int? GetEmptySlot();
    public void Add(Item item);
    public Item Find(Func<Item, bool> condition);
}
```

### 6.3 장비 스텟 계산

```csharp
// Player.cs
public override int TotalAttack
{
    get { return Stat.Attack + WeaponDamage; }  // 기본 + 무기
}

public void RefreshAdditionalStat()
{
    WeaponDamage = 0;
    ArmorDefence = 0;

    foreach (Item item in Inven.GetItems())
    {
        if (!item.Equipped) continue;

        switch (item.ItemType)
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
```

---

## 7. 전체 실행 흐름

### 7.1 서버 시작

```
Program.Main()
  │
  ├─ ConfigManager.LoadConfig()     // config.json
  ├─ DataManager.LoadData()         // Stat, Skill, Item, Monster JSON
  ├─ AppDbContext.EnsureCreated()   // SQLite DB 생성
  │
  ├─ GameLogic.Instance.Add(1)      // 게임 방 생성
  │   └─ GameRoom.Init()
  │       ├─ Map.LoadMap(1)
  │       ├─ Zone 그리드 생성
  │       └─ 몬스터 500마리 생성
  │
  ├─ Listener.Init(7777)            // 포트 수신 시작
  │
  ├─ DbTask 스레드 시작
  ├─ NetworkTask 스레드 시작
  │
  └─ GameLogicTask()                // 메인 스레드 무한 루프
```

### 7.2 클라이언트 접속

```
TCP 연결 → Listener.OnAcceptCompleted()
         → SessionManager.Generate()
         → ClientSession.OnConnected()
         → S_Connected 전송
         → Ping 스케줄링 (5초 후)
```

### 7.3 게임 진행 (이동 예시)

```
C_Move 수신 → PacketHandler.C_MoveHandler()
           → room.Push(room.HandleMove)
           → [GameLogic 스레드에서]
           → GameRoom.HandleMove()
           → Map.CanGo() 검증
           → Map.ApplyMove()
           → Broadcast(S_Move)
           → [NetworkTask 스레드에서]
           → ClientSession.FlushSend()
           → 실제 네트워크 송신
```

---

## 8. 스레드 안전성

### 8.1 스레드별 역할

| 스레드 | 역할 | 동기화 방식 |
|--------|------|-------------|
| GameLogic (Main) | 게임 로직 처리 | JobSerializer |
| Recv (N개) | 패킷 수신 | RecvBuffer |
| NetworkSend (1개) | 배치 송신 | _lock |
| DB (1개) | 데이터베이스 | JobSerializer |

### 8.2 동기화 패턴

```csharp
// 1. Session 송신 (lock)
lock (_lock)
{
    _sendQueue.Enqueue(data);
}

// 2. GameRoom 작업 (JobSerializer)
room.Push(action);  // 다른 스레드에서 안전하게 추가
room.Flush();       // GameLogic 스레드에서만 실행

// 3. DB 작업 (스레드 분리 + 콜백)
DbTransaction.Instance.Push(() => {
    // DB 스레드
    db.SaveChanges();
    room.Push(() => { /* GameRoom 스레드 콜백 */ });
});
```

---

## 9. 성능 최적화 기법

### 9.1 패킷 배치 송신
- 100ms 또는 10KB 도달 시 일괄 송신
- 네트워크 오버헤드 감소

### 9.2 Zone 공간 분할
- 1000x1000 맵 → 100개 Zone (10x10)
- Broadcast 시 인접 9개 Zone만 확인

### 9.3 Vision Culling
- 5칸 범위 외 객체는 무시
- Spawn/Despawn 최소화

### 9.4 몬스터 AI 최적화
- 200ms마다 업데이트 (프레임마다 X)
- 플레이어 탐색 1초마다

---

## 주요 설계 패턴

| 패턴 | 사용처 | 목적 |
|------|--------|------|
| Singleton | GameLogic, DbTransaction | 전역 인스턴스 |
| Factory | SessionManager.Generate() | 동적 생성 |
| FSM | Monster AI | 상태별 동작 |
| Observer | VisionCube | 시야 변화 감지 |
| Command | Job, IJob | 작업 큐잉 |
| IOCP | 비동기 소켓 | 논블로킹 I/O |

---

## 학습 포인트

1. **비동기 소켓 프로그래밍** - `SocketAsyncEventArgs`, IOCP
2. **멀티스레드 설계** - JobSerializer, lock, 스레드 분리
3. **게임 서버 아키텍처** - Zone, Vision, Broadcast
4. **Entity Framework** - Code First, 비동기 DB 처리
5. **Protobuf** - 효율적인 직렬화
6. **디자인 패턴** - Singleton, Factory, FSM, Command

이 프로젝트는 MMO 게임 서버의 핵심 개념을 모두 포함하고 있어 학습용으로 적합합니다.
