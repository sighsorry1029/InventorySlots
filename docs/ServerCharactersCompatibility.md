# ServerCharacters Compatibility Review

분석 대상:

- `Libs/ServerCharacters.dll`
- 플러그인 GUID: `org.bepinex.plugins.servercharacters`
- 표시 이름/버전: `Server Characters` / `1.4.16`

## 결론

`ServerCharacters`는 캐릭터 저장의 권위를 서버에 두는 모드다. 클라이언트가 접속하면 서버가 저장한 `PlayerProfile` 바이트를 내려주고, 클라이언트는 그 프로필을 `Game.instance.m_playerProfile`에 덮어쓴 뒤 플레이어를 스폰한다. 저장 시에는 클라이언트의 `PlayerProfile.SavePlayerToDisk` 결과 바이트를 서버로 보내고, 서버는 이를 서버 캐릭터 파일로 저장한다.

`InventorySlots` 입장에서는 하드 의존성을 걸 필요는 없다. Valheim의 표준 `Player.Save`, `Player.Load`, `Inventory.Save`, `Inventory.Load` 경로를 안정적으로 유지하는 편이 더 안전하다. 다만 서버가 권위 저장소이므로, 로컬 백업 복구가 서버 프로필 위에 끼어들면 안 된다.

이번 호환 처리에서는 다음을 적용했다.

- `ServerCharacters.dll`을 `Libs`에 참고용으로 포함했다. 빌드/배포 의존성으로 복사하지 않는다.
- `ServerCharacters`가 활성화된 멀티플레이에서는 `InventorySlotsBackup` 로컬 백업 저장/복구를 사용하지 않는다.
- 저장 직전에는 백업 여부와 무관하게 `EnsureInventoryState()`를 먼저 실행해 슬롯 높이와 좌표를 정규화한다.
- `Game.SpawnPlayer` 후순위 postfix를 추가해 `ServerCharacters`의 서버 프로필/신규 캐릭터 템플릿 적용 이후 한 번 더 인벤토리 상태를 안정화한다.
- ServerSync 초기 동기화 전에는 파괴적인 슬롯 검증을 미룬다. 내부 인벤토리 높이 보정은 하되, 커스텀 슬롯 정의가 아직 도착하지 않은 상태에서 슬롯 아이템을 “잘못된 위치”로 오판해 이동시키지 않기 위함이다.

## ServerCharacters 저장/로드 구조

접속 흐름:

1. 서버 `ZNet.RPC_PeerInfo` 패치가 접속자의 서버 저장 프로필을 찾는다.
2. 서버는 `ServerCharacters PlayerProfile` RPC로 압축된 `PlayerProfile` 바이트를 클라이언트에 보낸다.
3. 클라이언트는 받은 바이트를 `PlayerProfile.LoadPlayerProfileFromBytes()`로 읽는다.
4. 정상 프로필이면 `Game.instance.m_playerProfile`을 서버 프로필로 교체한다.
5. 신규 캐릭터라면 `Game.SpawnPlayer` postfix에서 인벤토리/스킬/지식/customData를 초기화하고 템플릿을 적용한다.

저장 흐름:

1. `PlayerProfile.SavePlayerToDisk` transpiler가 저장 바이트를 가로챈다.
2. 클라이언트는 해당 바이트를 `ServerCharacters PlayerProfile` RPC로 서버에 보낸다.
3. 서버는 받은 바이트를 서버 캐릭터 파일로 저장하고 이전 파일을 백업한다.
4. 클라이언트 인벤토리가 바뀌면 `Inventory.Changed` prefix가 다음 프레임에 `Inventory.Save` 바이트를 `ServerCharacters PlayerInventory` RPC로 보낸다.
5. 서버는 최신 인벤토리 바이트를 메모리에 보관하다가 disconnect 때 저장된 프로필의 inventory 구간만 교체한다.

중요한 점은 `ServerCharacters`가 아이템을 별도 슬롯 의미로 해석하지 않고, 기본 `Inventory.Save/Load` 바이트를 보존한다는 점이다. 따라서 `InventorySlots`의 고정 높이, 슬롯 tail 좌표, item customData가 저장 전에 정상화되어 있으면 서버 저장에도 그대로 들어간다.

## InventorySlots 관점의 안정성

유리한 부분:

- `InventorySlots`는 extra rows와 equipment/quick/custom slots를 하나의 player inventory 안에서 고정 좌표로 관리한다.
- extra rows는 max row를 미리 예약하고 locked row만 숨김/차단하므로, 진행도 변화가 장비 슬롯 좌표를 밀지 않는다.
- custom equipment slot 정보는 `ItemData.m_customData`에 보관되며, `Inventory.Save`에 포함되는 경로를 탄다.
- ServerCharacters는 최신 인벤토리만 별도로 서버에 보내는 레이어가 있어서, inventory 변경 직후 disconnect에도 상대적으로 강하다.

주의할 부분:

- ServerCharacters가 서버 프로필을 내려주는 타이밍과 다른 모드의 ServerSync 동기화 타이밍이 항상 직관적이지는 않다.
- 커스텀 슬롯 정의 YAML이 아직 서버 값으로 동기화되기 전에 tail 영역을 강하게 검증하면, 정상 슬롯 아이템을 invalid로 판단할 수 있다.
- 그래서 InventorySlots는 ServerSync 초기 동기화 전에는 슬롯 검증/이동을 미루고, 높이 보정만 수행하도록 했다.

## 백업/복구 정책

`InventorySlotsBackup`은 일반 환경에서 마지막 방어선으로 의미가 있다. 하지만 `ServerCharacters` 환경에서는 서버 저장 프로필이 권위 데이터다.

따라서 ServerCharacters 활성 멀티플레이에서는:

- 로컬 `InventorySlotsBackup` 복구를 하지 않는다.
- 로컬 `InventorySlotsBackup` 저장도 하지 않는다.
- 대신 `Player.Save` 직전 `EnsureInventoryState()`는 계속 수행한다.

이 방식이 더 안전한 이유:

- 오래된 로컬 백업이 서버 권위 프로필을 덮어쓰는 경로를 차단한다.
- `m_customData`에 압축 인벤토리를 중복 저장하지 않아 프로필 크기와 네트워크 전송량이 줄어든다.
- ServerCharacters 자체 emergency backup/signature 흐름과 역할이 겹치지 않는다.

## 테스트 체크리스트

운영 서버 투입 전에는 다음 경로를 한 번씩 확인하는 것이 좋다.

- 서버 접속 후 equipment/custom/quick slot 아이템 장착
- 인벤토리 변경 직후 로그아웃 후 재접속
- 인벤토리 변경 직후 서버 강제 재시작 또는 클라이언트 비정상 종료
- 사망 후 tombstone 생성, take all, 재접속
- 커스텀 YAML 슬롯 정의를 추가한 상태에서 재접속
- ServerCharacters `backupOnlyMode` off/on 각각에서 저장 확인

현재 코드 구조 기준으로는 별도 하드 의존성이나 ServerCharacters 타입 참조 없이 호환하는 편이 가장 안전하다. 필요한 최적화는 “저장 직전 정규화”, “서버 권위 환경에서 로컬 복구 차단”, “동기화 전 파괴적 검증 지연” 세 가지면 충분해 보인다.
