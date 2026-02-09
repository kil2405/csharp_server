# C# 게임 서버 프로젝트

## 프로젝트 개요
- **Git**: https://github.com/kil2405/csharp_server.git
- **Framework**: .NET 8.0
- **직렬화**: Google Protobuf
- **DB**: SQLite (Entity Framework Core)
- **상태**: 활성 개발 중

## 기술 스택
- .NET 8.0, Google.Protobuf, EF Core + SQLite
- SocketAsyncEventArgs (IOCP 비동기 소켓)

## 프로젝트 구조
```
csharp_server/
├── Server/                       # 솔루션 루트 (Server.sln)
│   ├── Server/                   # 게임 서버 메인
│   │   ├── Program.cs            # 엔트리포인트 (스레드 배치)
│   │   ├── DB/                   # AppDbContext, DataModel, DbTransaction
│   │   ├── Data/                 # ConfigManager, DataManager, Data.Contents
│   │   ├── Game/
│   │   │   ├── Object/           # GameObject, Player, Monster, Arrow
│   │   │   ├── Room/             # GameRoom, Map, Zone, VisionCube
│   │   │   ├── Item/             # Inventory, Item 시스템
│   │   │   └── Job/              # JobSerializer (스레드 안전 큐)
│   │   ├── Packet/               # 패킷 핸들러 + Protocol
│   │   ├── Session/              # ClientSession, SessionManager
│   │   └── Utils/
│   ├── ServerCore/               # 저수준 네트워크 라이브러리
│   │   ├── Session.cs            # 비동기 소켓 세션
│   │   ├── Listener.cs           # TCP 리스너
│   │   ├── Connector.cs          # 클라이언트 커넥터
│   │   └── RecvBuffer.cs         # 수신 링 버퍼
│   ├── DummyClient/              # 테스트 더미 클라이언트
│   └── PacketGenerator/          # Protobuf 패킷 코드 생성기
```

## 아키텍처

### 스레드 모델
- **GameLogic (메인)**: GameRoom.Update() 무한 루프
- **Recv (N개)**: SocketAsyncEventArgs 자동 관리
- **Send (1개)**: 100ms/10KB 배치 송신 (ClientSession.FlushSend)
- **DB (1개)**: DbTransaction.Flush()

### 핵심 패턴
- **JobSerializer**: lock-free push + single-thread flush (GameRoom, DbTransaction)
- **Zone 공간 분할**: 1000x1000 맵 -> 10x10 Zone, Broadcast 시 인접 9개만
- **VisionCube**: 5칸 시야, 100ms 갱신, Spawn/Despawn 최소화
- **Monster FSM**: Idle -> Moving -> Skill -> Dead (200ms 틱)
- **패킷 포맷**: [Size(2)][MsgId(2)][Protobuf Data]

### 패킷 처리 흐름
```
수신 -> PacketManager.OnRecvPacket() -> MakePacket<T>()
-> PacketHandler -> room.Push(handler) -> GameRoom 처리 -> Broadcast
```

### DB 처리 (스레드 분리)
```
GameRoom 스레드: 데이터 준비 -> DbTransaction.Push()
DB 스레드: SaveChanges() -> room.Push(콜백)
```

## 코딩 규칙
- namespace: Server, Server.Game, Server.DB 등
- 탭 인덴트
- 멤버 변수 `_` 접두사 (`_socket`, `_recvBuffer`)
- partial class 사용 (GameRoom_Battle.cs, GameRoom_Item.cs)
- Singleton: `GameLogic.Instance`, `DbTransaction.Instance`
- DB 작업은 반드시 DbTransaction 경유

## 빌드 및 실행
```bash
cd Server && dotnet build
dotnet run --project Server/Server.csproj
```

## 에이전트 규칙 (필수)
1. **작업 전 `git pull` 실행**
2. **변경 후 README.md History + agent TASKS.md 업데이트**
3. **커밋 전 History 업데이트 필수**
4. **GameDB.db 파일 커밋 금지 (.gitignore)**
5. **ServerCore 수정 시 주의**: Server와 DummyClient 모두 참조
