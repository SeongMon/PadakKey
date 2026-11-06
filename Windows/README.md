# PadakKey for Windows (preview)

Windows 11 x64용 트레이 앱입니다. Windows 10은 현재 실제 기기 검증 대상에 포함되지 않습니다.

## 실행

GitHub Actions의 **Windows build and checks** 성공 실행 → **PadakKey-Windows-x64** artifact를 다운로드하고 ZIP 전체를 폴더에 풀어 `PadakKey.exe`를 실행하세요. .NET 런타임은 포함됩니다. 최상단에는 `PadakKey.exe`와 `Assets` 폴더만 있습니다. DLL과 .NET 런타임은 실행 파일에 묶었습니다. `Assets` 폴더는 실행 파일 옆에 유지하세요. 코드 서명되지 않은 개발 빌드로 Windows가 게시자 확인 경고를 표시할 수 있습니다.

오른쪽 아래 `^`에서 참새 아이콘을 클릭하면 OTP 창이 열립니다. 아이콘을 항상 표시할지는 Windows 설정에서 결정합니다. 숫자를 클릭하면 복사하고 창을 닫습니다. 30초 후 클립보드 내용이 다른 앱에 의해 바뀌지 않았을 때만 지웁니다.

- + 버튼: QR 이미지 또는 수동 Base32 키 / otpauth URI 등록
- 트레이 오른쪽 클릭 또는 창의 설정: 파닥임 / 졸음 선택, 애니메이션 ON/OFF, 아이콘 크기 60~100%, PNG/GIF 추가
- Windows 트레이 칸의 크기 자체는 OS가 정합니다. 100%가 칸 내부 최대 크기입니다.
- 설정과 아이콘: `%LOCALAPPDATA%\PadakKey`
- 계정: 같은 위치의 `accounts.dpapi`, 현재 Windows 사용자에 묶인 DPAPI 암호화. 원본 키를 평문 파일로 저장하지 않습니다.
- Mac 계정을 자동으로 동기화하거나 가져오지는 않습니다. 각 서비스에서 등록 키를 따로 추가해야 합니다. Windows 사용자/PC 변경 시 파일만 복사하는 방식으로 복구할 수 없습니다.
- HOTP와 Google Authenticator 일괄 이전 QR은 지원하지 않습니다.

## 개발 / 검사

.NET 10 SDK와 Windows가 필요합니다.

```
dotnet run --project Windows/PadakKey.Checks -c Release
dotnet build Windows/PadakKey -c Release
Windows\PadakKey\bin\Release\net10.0-windows\PadakKey.exe --self-test
dotnet publish Windows/PadakKey -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/PadakKey-Windows-x64
```

CI는 RFC6238의 SHA1/256/512 벡터, 입력 검증, DPAPI 저장/복원 및 손상 파일 거부, 생성한 QR의 이미지 해독, 아이콘 로딩을 확인합니다. 실제 OTP 데이터는 테스트에 사용하지 않습니다.

## 실제 Windows에서 확인할 항목

- 트레이 숨김 영역과 항상 표시 상태에서 열기, 바깥 클릭 및 복사 후 닫기
- 100/150/200% 배율, 다중 모니터, 화면 가장자리의 창 위치
- 두 애니메이션의 ON/OFF, 크기 조절, 사용자 PNG/GIF, 재실행 후 설정 유지
- 서비스의 OTP로 실제 로그인, PC 시간 자동 동기화
- QR 이미지 선택 취소/실패, 계정 추가·삭제, 손상 파일 복구 안내
- 잠금/해제, 클립보드 변경 및 30초 자동 삭제, 종료·중복 실행

Windows GUI 동작은 Mac에서 직접 검증하지 않았습니다. 로그인 시 자동 시작과 ARM64 패키지는 이 초기 버전에 포함하지 않았습니다.
