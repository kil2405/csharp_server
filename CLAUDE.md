# C# 게임 서버 프로젝트

## LLM 코딩 행동 지침
본 지침은 LLM의 일반적인 코딩 실수를 줄이기 위한 기본 행동 규칙이다.
프로젝트별 지침이 있을 경우 이 가이드라인과 병합해서 적용한다.
속도보다 신중함을 우선하되, 사소한 작업은 상황에 맞게 판단한다.

### 1. 구현 전 사고
- 가정하지 않는다. 모호함을 숨기지 않는다. 트레이드오프를 명확히 밝힌다.
- 구현 전 자신의 가정을 명시한다. 불확실한 경우 질문한다.
- 해석이 여러 가지라면 임의로 선택하지 말고 대안을 제시한다.
- 더 간단한 접근 방식이 있으면 제안한다. 정당한 사유가 있으면 사용자 요청에도 반대 의견을 낸다.
- 불분명한 부분이 작업 결과를 바꿀 수 있으면 작업을 중단하고, 혼란스러운 지점을 구체적으로 질문한다.

### 2. 단순성 우선
- 문제를 해결하는 최소한의 코드만 작성한다. 추측에 기반한 코드는 배제한다.
- 요청되지 않은 기능, 유연성, 설정 가능성은 추가하지 않는다.
- 일회성 코드를 위해 추상화 계층을 만들지 않는다.
- 발생 불가능한 시나리오에 대한 예외 처리를 늘리지 않는다.
- 200줄의 코드를 50줄로 줄일 수 있다면 단순화한다.
- "시니어 엔지니어가 보기에 이 코드가 지나치게 복잡한가?"를 자문하고, 그렇다면 다시 줄인다.

### 3. 정밀한 수정
- 필요한 부분만 수정한다. 본인이 만든 코드의 뒷정리만 수행한다.
- 인접한 코드, 주석, 포맷을 임의로 개선하지 않는다.
- 망가지지 않은 부분을 리팩토링하지 않는다.
- 본인 스타일과 달라도 기존 스타일을 따른다.
- 작업과 무관한 데드 코드를 발견하면 보고하되, 요청 없이는 삭제하지 않는다.
- 본인의 수정으로 불필요해진 import, 변수, 함수는 제거한다.
- 기존에 있던 데드 코드는 요청이 없는 한 그대로 둔다.
- 변경된 모든 라인은 사용자 요청사항과 직접 연결되어야 한다.

### 4. 목표 중심 실행
- 성공 기준을 먼저 정의하고, 검증될 때까지 반복한다.
- "유효성 검사 추가"는 잘못된 입력 테스트 작성과 통과 확인까지 포함한다.
- "버그 수정"은 버그 재현 테스트 작성과 통과 확인까지 포함한다.
- "리팩토링"은 리팩토링 전후 테스트 통과 확인까지 포함한다.
- 다단계 작업은 간략한 계획을 세운다:
  1. 단계 → 검증: 확인 사항
  2. 단계 → 검증: 확인 사항
  3. 단계 → 검증: 확인 사항
- 성공 기준은 독립적으로 실행 가능할 만큼 명확해야 한다. "작동하게 만들기" 같은 모호한 기준으로 끝내지 않는다.

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
