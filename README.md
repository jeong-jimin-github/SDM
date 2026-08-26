# SDM

Windows용 멀티커넥션 다운로드 매니저. 브라우저에서 받은 파일을 가로채고, 여러 HTTP 연결로 나눠 받습니다.

## 다운로드

[Releases](https://github.com/jeong-jimin-github/SDM/releases)에서 두 가지를 제공합니다.

| 파일 | 설명 |
|---|---|
| `SDM-Setup-YYYY-MM-DD.exe` | 설치 마법사 |
| `SDM-portable-YYYY-MM-DD.zip` | 압축만 풀면 실행되는 portable |

`main`에 push하면 GitHub Actions가 한국 시간 날짜 태그(`YYYY-MM-DD`)로 릴리스를 만듭니다. 같은 날 다시 push하면 해당 날짜 릴리스를 덮어씁니다.

## 구성

| 프로젝트 | 역할 |
|---|---|
| `src/SDM.Core` | 세그먼트 다운로드 엔진, 대기열, 재개, IPC |
| `src/SDM.App` | WPF 본체 (`SDM.exe`) |
| `src/SDM.NativeHost` | Chrome/Firefox Native Messaging 브리지 |
| `extensions/chrome` | Chrome / Edge / Brave 확장 |
| `extensions/firefox` | Firefox 확장 (`sdm@sdm.app`) |

## 기능

- HTTP(S) Range 분할 다운로드 (1–32 연결), 일시정지/재개, 이어받기
- 서버가 Range를 무시하면 단일 연결로 전환
- `Content-Disposition` · MIME · 파일 시그니처로 이름/확장자 보정
- 동시 작업 수 · 속도 제한 · 분류별 폴더
- 클립보드 URL 감지, 트레이, `sdm://` 프로토콜
- 브라우저 다운로드 가로채기, 우클릭 전송, 비디오/오디오 스니퍼

## 로컬 빌드

```powershell
dotnet build src\SDM.App\SDM.App.csproj -c Release
```

Windows 배포물:

```powershell
.\scripts\publish-windows.ps1
```

결과는 `artifacts\win-x64\` 입니다.

## 브라우저 연결

1. SDM 실행 → **브라우저 연결** → **지금 등록**
2. **Chrome / Edge / Brave**
   - `chrome://extensions` → 개발자 모드 ON
   - **압축해제된 확장 프로그램을 로드합니다**
   - `%LOCALAPPDATA%\SDM\extensions\chrome`
3. **Firefox**
   - `about:debugging#/runtime/this-firefox`
   - **임시 부가 기능 로드** → `%LOCALAPPDATA%\SDM\extensions\firefox\manifest.json`

연결 순서는 Native Messaging(`com.sdm.host`) → 실패 시 `http://127.0.0.1:47832`.

단축키: `Ctrl+N` 추가, `Space` 일시정지/재개, `Delete` 제거.

## 요구 사항

- Windows 10/11 x64
- 설치/portable 빌드는 .NET 런타임을 포함합니다. 소스 빌드 시 .NET 10 SDK가 필요합니다.
