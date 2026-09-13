# Container UI Diagnostics

Valheim 1.0.12 / BepInEx 5.4.2350용 임시 클라이언트 진단 플러그인입니다.

## 설치와 확인

1. 게임을 종료합니다.
2. `ContainerUiDiagnostics.dll` 하나를 **사용 중인 클라이언트 프로필**의 `BepInEx/plugins`에 넣습니다. 다른 하위 폴더에 같은 진단 DLL을 중복 설치하지 않습니다. 기존 `InventorySlots.dll`은 그대로 사용합니다.
3. 재실행 후 문제가 있는 상자를 2초 이상 열거나 미리보기로 표시합니다.
4. 해당 프로필의 `BepInEx/LogOutput.log`에서 `Container UI diagnostic:` 기록을 확인합니다. 가능하면 안 보이는 상태와 다시 보이는 상태를 같은 실행에서 기록합니다.
5. 진단 종료 후 게임을 끄고 `ContainerUiDiagnostics.dll`을 삭제하면 됩니다.

정상 초기화 시 `Standalone client container UI diagnostics active`, 월드 진입 후에는 `Container UI diagnostics sampling ready` 로그가 남습니다. BepInEx의 `Loading` 한 줄만으로 실제 측정이 시작됐다고 판단하지 않습니다.

LoadTimeProfiler의 기본 모드 로그 필터는 Info를 제외합니다. 이를 사용하는 프로필은 `BepInEx/config/sighsorry.LoadTimeProfiler.cfg`의 `[Logging.Mods]`에 아래 한 줄을 추가합니다.

```ini
sighsorry.ContainerUiDiagnostics = Errors, Warnings, Information
```

LoadTimeProfiler 1.3.2에서는 이 설정이 실시간 반영됩니다. 이미 지나간 초기화 로그는 다시 나오지 않으므로 상자를 닫았다 다시 열어 3초 정도 기다린 뒤 스냅샷을 확인합니다. 로딩 기록만 있고 진단 기록이 없다는 이유로 진단 플러그인의 초기화 실패를 단정하지 않습니다.

서버에는 설치하지 않습니다. 다른 클라이언트도 설치할 필요가 없으며, 증상을 재현하는 클라이언트에만 설치하면 됩니다.

## 범위

- 원본 게임 DLL을 참조하며 InventorySlots, InventoryActions, Harmony, ServerSync에 의존하지 않습니다.
- 별도 네트워크 통신·RPC·설정 동기화·게임 패치를 등록하지 않습니다.
- 인벤토리/아이템/소유권/UI 상태를 변경하지 않고 로그만 기록합니다. 빈 상자 표시를 고치는 플러그인은 아닙니다.
- 2초 간격으로 보이는 상자 패널을 검사합니다. 연속으로 같은 상태는 다시 기록하지 않습니다. 별도 설정 파일은 생성하지 않습니다.
- 상자와 그리드의 아이템 수·크기, 생성 슬롯 수, 활성 상태, 위치, 투명도, 스크롤, 마스크·Canvas 상태를 기록합니다. 아이템 저장 데이터나 플레이어 ID는 기록하지 않습니다.
- 미리보기의 상자는 표시 중인 Inventory와 마우스가 가리킨 상자의 참조를 대조합니다. 확인되지 않으면 `source=unresolved`로 기록합니다.
- 로컬 플레이어와 InventoryGui가 준비된 클라이언트에서만 샘플링합니다. 실행 중 데디케이트 서버에서는 샘플링하지 않습니다. 시작 직후 그래픽 장치가 아직 없다는 이유로 영구 비활성화하지 않습니다.

## 빌드

```powershell
dotnet build tools/ContainerUiDiagnostics/ContainerUiDiagnostics.csproj -c Debug -p:DeployToGame=true
```

`GamePath`로 원본 게임 설치 경로를 지정할 수 있습니다. `AdditionalPluginPath`를 지정하면 최종 진단 DLL만 해당 테스트 프로필에도 복사합니다. 참조 DLL은 복사하지 않습니다. ZIP·릴리스 패키지 생성 타깃은 없습니다.

검증: 원본 DLL 참조 Debug 빌드, DLL 정적 검사, 실제 초기화·샘플링 메서드를 스텁 환경에서 실행한 수명주기 검사 10개 통과. 사용자 데디케이트 접속에서 열린 상자와 미리보기의 진단 로그 출력을 확인했습니다. 이 결과는 컨테이너 표시 수정의 실제 실행 검증과 별개입니다.
