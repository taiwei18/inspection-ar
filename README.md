# 现场巡检问题标注

巡检人员用手机 AR 在真实水平面上放置标记并上报问题，Go 后端保存，管理人员在电脑网页上查看问题并修改处理状态。

```text
Unity AR (Android 真机) ──POST /api/issues──▶ Go 后端 (JSON 文件持久化) ◀──GET / PATCH── React 管理端 (浏览器)
```

| 目录 | 内容 |
|---|---|
| `backend/` | Go 后端，只用标准库 |
| `web/` | React + TypeScript 管理端（Vite） |
| `unity/` | Unity 项目（AR Foundation + ARCore） |
| `AI_LOG.md` | AI 使用记录 |

## 使用的版本

| 部分 | 版本 |
|---|---|
| Go | 1.22 及以上（用到 1.22 的路由语法），开发和测试使用 1.27.0 |
| Node.js | 22.x（开发使用 22.23） |
| React / TypeScript / Vite | 19.3 / 7.0 / 8.3 |
| Unity | 6000.0 LTS（Unity 6），内置渲染管线 |
| AR Foundation / ARCore XR Plugin | 6.0.3 / 6.0.3 |
| Input System | 1.11.2 |
| 目标设备 | Android 7.0（API 24）及以上、支持 ARCore 的手机 |

## 启动

### 1. Go 后端

```bash
cd backend
go run .
```

- 默认监听 `:8080`（所有网卡），数据保存在 `backend/data/issues.json`，重启后自动加载。
- 启动日志会列出本机局域网地址，例如 `Unity 真机可使用: http://192.168.0.102:8080  (WLAN)`，括号里是网卡名，选 WLAN/以太网那一个，`vEthernet` 一类是虚拟网卡。
- 可选参数：`-addr :8080`、`-data data/issues.json`、`-cors-origin *`（也可以用环境变量 `ADDR` / `DATA_FILE` / `CORS_ORIGIN`）。
- 运行测试：`go test ./...`
- Windows 第一次启动时如果弹出防火墙提示，要允许「专用网络」，否则手机连不上。

### 2. React 管理端

```bash
cd web
npm install
npm run dev
```

打开 <http://localhost:5173>。开发服务器把 `/api` 代理到 `http://127.0.0.1:8080`，浏览器只访问同源地址，不存在跨域问题；后端也带了 CORS 头，可以设置 `VITE_API_BASE=http://IP:8080` 直接跨域访问。后端在其他电脑时：`VITE_PROXY_TARGET=http://IP:8080 npm run dev`。

### 3. Unity 客户端（Android + ARCore）

需要 Unity Hub + Unity 6000.0 LTS（安装时勾选 Android Build Support，包括 OpenJDK、Android SDK & NDK），以及一部开启了 USB 调试、支持 ARCore 的 Android 手机。

1. Unity Hub → Add → Add project from disk → 选择 `unity/` 目录（如果提示版本不一致，选已安装的 6000.0.x）。首次打开会自动下载 AR Foundation 等包。
2. 如果弹出启用新输入系统（enable the new input system backends）的对话框，选 **Yes**，编辑器会重启。
3. 菜单 **Inspection AR → 1. 配置 Android 构建参数**：设置包名、IL2CPP + ARM64、最低 API 24、OpenGLES3、允许 HTTP 明文请求。
4. **Edit → Project Settings → XR Plug-in Management → Android 页签 → 勾选 ARCore**；再打开其中的 Project Validation，如果有提示就点 **Fix All**。
5. 菜单 **Inspection AR → 2. 生成 AR 场景**：自动生成 `Assets/InspectionAR/Scenes/InspectionAR.unity`、平面和标记预制体，并加入构建场景列表。
6. File → Build Profiles → Android → Switch Platform，连上手机后点 **Build And Run**。

> 如果第 5 步提示找不到 XR Origin 菜单：先在 Hierarchy 中右键 **XR → XR Origin (Mobile AR)**，再执行一次第 5 步。

## Unity 真机如何配置 Go 后端地址

1. 手机和电脑连同一个 Wi-Fi（如果公司网络开了 AP 隔离，可以用电脑开热点）。
2. 在 Go 启动日志里找到 `Unity 真机可使用: http://192.168.x.x:8080`。
3. 把这个地址填到 App 顶部的输入框，点「测试连接」，出现「后端连接正常」即可。
   - 可以只填 `192.168.x.x:8080`，会自动补全 `http://`；
   - 地址保存在 PlayerPrefs，App 重启后仍然有效；默认值在 `BackendSettings.DefaultUrl` 中修改。
4. 连不上时依次检查：IP 是否正确、电脑防火墙是否放行 8080 端口、手机浏览器能否打开 `http://IP:8080/api/health`。

## 接口说明

前缀统一为 `/api`，请求和响应都是 UTF-8 编码的 JSON。

| 方法 | 路径 | 说明 | 成功响应 |
|---|---|---|---|
| GET | `/api/health` | 服务状态 | `200 {"status":"ok","time":"…","issueCount":3}` |
| GET | `/api/issues` | 问题列表，新的在前 | `200 {"items":[Issue, …]}` |
| POST | `/api/issues` | 创建问题（Unity 上报） | `201 Issue` |
| GET | `/api/issues/{id}` | 查询单个问题 | `200 Issue` |
| PATCH | `/api/issues/{id}` | 修改状态，请求体 `{"status":"in_progress"}` | `200 Issue` |

创建请求：

```json
{ "title": "入口墙面破损", "description": "左侧墙体存在裂缝", "priority": "high", "position": { "x": 0.42, "y": 0.03, "z": 1.26 } }
```

- `title` 必填，去掉首尾空格后不能为空，最多 100 字；`description` 可选，最多 1000 字。
- `priority` 只能是 `low` / `medium` / `high`；`position` 必填。
- `id`（UUID v4）、`status`（固定为 `open`）、`createdAt` / `updatedAt`（RFC 3339，UTC）由后端生成。
- 坐标保留 3 位小数（毫米），单位是米，坐标系是 AR 会话坐标系。

错误响应统一为：

```json
{ "error": { "code": "invalid_argument", "message": "title 不能为空", "field": "title" } }
```

| HTTP | code | 场景 |
|---|---|---|
| 400 | `invalid_argument` | 标题为空、枚举值错误、字段类型错误、缺少坐标、文本不是 UTF-8 |
| 400 | `invalid_json` | 请求体不是合法 JSON |
| 404 | `not_found` | ID 不存在 |
| 500 | `internal` | 写数据文件失败等 |

## 实现要点

- **持久化**：JSON 文件。每次写操作先写临时文件再重命名替换，进程中途退出也不会留下写了一半的文件；先写盘成功再更新内存；数据文件损坏时拒绝启动，避免被空数据覆盖。
- **点按钮不误放标记**：放置前用 `EventSystem.RaycastAll` 检查触点下有没有 UI；输入框正在编辑或软键盘打开时也忽略点击（`ArTapPlacer.IsBlockedByUi`）。
- **只放在水平面**：`ARPlaneManager` 只检测水平面，射线检测使用 `PlaneWithinPolygon`，并再次检查平面朝向。
- **标记稳定**：标记挂 `ARAnchor`，跟踪校正时不会漂移；待提交为黄色并闪动，提交后按优先级着色（红 / 橙 / 绿）。
- **提交反馈**：成功或失败都有提示条；失败时保留标记和表单内容，可以直接重试；后端返回的错误信息（如「title 不能为空」）原样显示。
- **React 不白屏**：请求有 8 秒超时，后端不可用时显示错误条和重试按钮，已加载的数据继续显示；另有 ErrorBoundary 兜底渲染异常。

## 完成情况

### 已完成

- 必做功能：三端全部实现，详见上文。
- 额外功能：
  - React：每 5 秒自动刷新并高亮新上报的问题，可按状态 / 优先级筛选、搜索，有状态统计；
  - Unity：标记颜色随优先级变化，在 App 内配置并测试后端地址；
  - Go：单元测试（存储、校验、接口、CORS），访问日志显示请求来源 IP，启动时列出可填给手机的地址。

### 未完成

- 登录与权限、实时推送（用轮询代替）、Unity 端历史问题列表与编辑、跨会话 / 跨设备定位（如 Cloud Anchors）、iOS 版本、Docker 部署。

### 已知问题与限制

- **Unity 部分尚未在 Unity 编辑器和真机上验证**：开发这些代码的电脑上没有安装 Unity，C# 只针对 Unity API 桩代码做过编译检查（语法、类型、互相引用）。真实 API 签名、场景生成菜单和真机效果需要在 Unity 6000.0 中打开后确认。<!-- 验证后更新这一条 -->
- 界面使用 Unity 内置字体，中文依赖手机系统字体回退；如果个别机型中文显示为方框，需要导入中文字体。
- Unity 包固定为 AR Foundation 6.0.3。使用更新的 Unity 6.x 时，可以在 Package Manager 中把 AR Foundation 和 ARCore XR Plugin 升级到同一个版本。
- 坐标只在本次 AR 会话内有意义，App 重启后原点会变化（任务书不要求恢复）。
- JSON 文件存储每次全量重写，适合原型级数据量；不支持多个后端实例同时写同一个文件。
- 没有鉴权，CORS 默认允许任意来源，只适合局域网演示。
- 在 Windows 上用 curl 或 PowerShell 5 测试接口时，命令行里的中文可能按 GBK 发送，后端会返回 400「请使用 UTF-8 编码提交」。可以把请求体存成 UTF-8 文件后用 `--data-binary @file.json` 发送，或改用 PowerShell 7。
