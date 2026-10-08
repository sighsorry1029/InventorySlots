# LOD Prefab Diagnostics

Unity의 `Renderer ... is registered with more than one LODGroup` 경고를 조사하는 임시 **클라이언트 전용** BepInEx 플러그인입니다. InventorySlots 설치 여부와 관계없이 동작합니다. Renderer/LOD 설정이나 아이템·저장·네트워크 상태를 수정하지 않습니다.

## 사용

1. 게임을 종료합니다.
2. `bin/Debug/LodPrefabDiagnostics.dll`을 문제가 발생하는 **실제 실행 프로필**의 `BepInEx/plugins` 폴더에 넣습니다. Gale 프로필과 Steam 기본 설치 폴더는 서로 다릅니다. BepInExPack Valheim 5.4.2351 환경을 기준으로 빌드했습니다.
3. 게임에 접속하여 문제가 있던 장비를 착용하거나 해당 지역에 접근합니다.
4. `BepInEx/LogOutput.log`에서 `LOD Prefab Diagnostics`, `LOD duplicate`, `LOD scan`을 확인합니다. 분석을 요청할 때는 로그 전체를 첨부합니다.
5. 재현 로그를 확보한 뒤 게임을 종료하고 이 진단 DLL을 제거합니다. 서버에는 설치하지 않습니다.

별도 키 조작이나 config 파일은 없습니다. ObjectDB 준비 후 초기 검사 한 번, 이후 해당 Unity 경고가 발생할 때만 최대 2초에 한 번 검사합니다. 일반 플레이용 최적화 모드가 아니므로, 검사 순간에는 추가 비용이 듭니다. 핵심 출력은 Info를 숨기는 로그 필터에서도 확인할 수 있도록 Warning으로 남깁니다.

## 출력 해석

- `LOD duplicate`: 동일 Renderer가 서로 다른 LODGroup에 등록된 것을 실제 검사에서 확인했습니다. 같은 그룹 안의 여러 LOD 단계에 반복되는 것은 정상으로 취급합니다.
- `source=attach-observed prefab=...`: 게임의 `VisEquipment.AttachItem`/`AttachArmor` 호출에서 관찰한 아이템 hash와 ObjectDB 프리팹 이름입니다. variant, quality, 착용 주체와 모델 경로도 출력합니다. 다른 모드가 외형을 치환할 수 있으므로 이 이름만으로 에셋을 만든 모드의 책임을 확정하지 않습니다.
- `source=containing-network-prefab`: ZDO에서 확인한 상위 네트워크 객체의 프리팹입니다. `Player` 등 상위 객체일 수 있으며, 내부 장비 프리팹을 확인했다는 뜻은 아닙니다.
- `source=unknown`: 프리팹 출처는 미확인입니다. 대신 Renderer·LODGroup의 전체 계층 경로, mesh 이름과 instance ID를 제공합니다.
- `found no duplicate at scan time`: 경고 이후 검사 전에 객체가 사라지거나 변경되었을 수 있습니다. 이때 최근 장착 기록은 참고 정황이며 원인 확정이 아닙니다. 로드된 장면에 속하지 않는 프리팹 에셋은 검사 대상에서 제외합니다.

동일 객체·그룹 조합의 상세 로그는 반복하지 않고, 한 검사에서 새로운 상세 로그를 최대 20개 기록합니다. 남은 수는 `deferredDetails`로 표시하고 다음 경고 검사에서 계속 기록합니다. 장시간 세션의 진단 기록은 제한된 크기로 유지합니다.

## 빌드와 검증 범위

```powershell
dotnet build tools/LodPrefabDiagnostics/LodPrefabDiagnostics.csproj -c Debug -p:DeployToGame=true
dotnet run --project tools/LodPrefabDiagnostics/Tests -c Debug
```

원본 Valheim 1.0.17 클라이언트 DLL을 참조하며 publicizer를 사용하지 않습니다. 비공개 장착 메서드는 시그니처를 검사하여 Harmony Postfix로 관찰합니다. Unity 로그 콜백은 카운터만 변경하고 실제 객체 검사는 메인 스레드에서 수행합니다. 종료·진단 중단 시 이벤트와 자체 패치를 해제합니다.

`DeployToGame=true`는 최종 진단 DLL 한 개만 Steam Valheim의 `BepInEx/plugins`에 복사합니다. Gale 프로필에는 별도로 넣어야 합니다. 자동 검사는 그룹 구분과 대상 메서드 계약을 검사하며 실제 Unity 장면 재현 검증을 대신하지 않습니다. Release/ZIP 생성 경로는 없습니다.
