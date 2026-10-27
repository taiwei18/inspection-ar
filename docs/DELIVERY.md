# 验收与演示指南

对照任务书逐项列出完成情况、核心代码位置和演示视频的录制步骤。「已验证」只用于实际跑过的项目；还没有录屏或真机确认的项目，标为「待视频确认」。

## 1. 交付清单（任务书第 6 节）

| 交付物 | 位置 | 状态 |
|---|---|---|
| Unity、React、Go 完整源码 | `unity/`、`web/`、`backend/` | 已完成 |
| README（版本、启动、真机配置后端地址、完成情况） | [README.md](../README.md) | 已完成 |
| AI 使用记录 | [AI_LOG.md](../AI_LOG.md) | 已记录到当前为止的全部 Prompt，后续继续追加 |
| 手机演示视频（3～5 分钟） | — | 未录制 |
| 电脑演示视频（3～5 分钟） | — | 未录制 |
| 分享链接（GitHub / Gitee / 网盘） | — | 未创建 |
| APK（可选） | `unity/Builds/InspectionAR.apk`（不入库） | 已构建，可选附带 |

## 2. 必做功能逐项验收

### 2.1 Unity AR 移动端（任务书 3.1）

| # | 要求 | 实现位置 | 验证方式 | 状态 |
|---|---|---|---|---|
| 1 | 支持 AR 的真机运行 | Android + ARCore；[InspectionProjectSetup.cs](../unity/Assets/InspectionAR/Editor/InspectionProjectSetup.cs) 配置构建参数 | vivo X200s（Android 15）安装运行 | 已验证 |
| 2 | 检测真实水平面 | `ARPlaneManager.requestedDetectionMode = Horizontal`（场景生成时设置） | 真机看到平面网格 | 已验证 |
| 3 | 点击平面放置标记 | [ArTapPlacer.cs](../unity/Assets/InspectionAR/Scripts/AR/ArTapPlacer.cs) 射线检测 → [InspectionAppController.cs](../unity/Assets/InspectionAR/Scripts/InspectionAppController.cs) `OnPlaneTapped` 创建锚点和标记 | 真机放置 | 已验证 |
| 4 | 填写并提交标题 / 描述 / 优先级 / 坐标 | [IssueReportUI.cs](../unity/Assets/InspectionAR/Scripts/UI/IssueReportUI.cs) 表单、`Submit` 组装请求 | 后端收到手机提交的「袜子」「电脑」「衣服」 | 已验证 |
| 5 | 提交成功 / 失败有明确提示 | `OnSubmitSucceeded` / `OnSubmitFailed` → `ShowToast` | 成功提示已在真机出现；失败提示需在视频中停掉后端后演示 | 成功已验证，失败待视频确认 |
| 6 | 点按钮或输入框时不误放标记 | `ArTapPlacer.IsBlockedByUi`：UI 射线检测、输入框聚焦、软键盘可见三重判断 | 视频中专门点按钮和输入框演示 | 待视频确认 |

### 2.2 React 管理端（任务书 3.2）

| # | 要求 | 实现位置 | 验证方式 | 状态 |
|---|---|---|---|---|
| 1 | 从后端获取问题列表 | [api.ts](../web/src/api.ts) `listIssues`，[useIssues.ts](../web/src/hooks/useIssues.ts) `reload` | 浏览器打开 `localhost:5173`；手机提交后出现 | 已验证 |
| 2 | 至少显示标题、优先级、状态 | [IssueTable.tsx](../web/src/components/IssueTable.tsx)，另显示描述、坐标、时间 | 浏览器查看 | 已验证 |
| 3 | 状态改为 open / in_progress / resolved | 行内下拉框或详情抽屉 → `updateIssueStatus`（PATCH） | 浏览器操作，刷新后保持 | 已验证 |
| 4 | 后端不可用时显示错误，不白屏 | `reload` 捕获错误 → 错误提示框和重试按钮；`ErrorBoundary` 兜底 | 停掉 Go 后页面显示「无法加载问题列表」 | 已验证 |

### 2.3 Go 后端（任务书 3.3）

| # | 要求 | 实现位置 | 验证方式 | 状态 |
|---|---|---|---|---|
| 1 | 服务运行状态检查 | `GET /api/health` | `TestHealth` | 已验证 |
| 2 | 接收并保存 Unity 上报 | `POST /api/issues` → `Store.Create` | `TestCreateListAndUpdateStatus`；真机上报 | 已验证 |
| 3 | 返回问题列表 | `GET /api/issues` | 同上；`TestEmptyListIsArrayNotNull` | 已验证 |
| 4 | 按 ID 修改状态 | `PATCH /api/issues/{id}` → `Store.UpdateStatus` | `TestUpdateStatus`、`TestUpdateStatusErrors` | 已验证 |
| 5 | 生成唯一 ID，默认 `open` | `newUUID`（UUID v4）、`Create` 固定 `StatusOpen` | `TestCreateGeneratesUniqueIDsAndListsNewestFirst` | 已验证 |
| 6 | 标题为空、枚举错误返回明确参数错误 | `CreateInput.Normalize`、`Status.Valid` → 400 `invalid_argument` 并带 `field` | `TestCreateValidation`、`TestCreateValidationErrors` | 已验证 |
| 7 | 重启后数据仍在 | JSON 文件持久化，启动时 `OpenStore` 加载 | `TestCreatePersistsAcrossReopen`；手工停止再启动 Go | 已验证 |
| 8 | Unity 与 React 共用同一后端和数据 | 同一个 Go 进程和 `data/issues.json` | 手机提交的记录出现在网页 | 已验证 |
| 9 | React 在浏览器中能访问后端 | Vite 代理 `/api` + 后端 CORS | `TestCORSPreflight`；浏览器访问 | 已验证 |

## 3. 核心代码说明

任务书要求能解释最终提交的核心代码。下面按请求经过的顺序说明。

### 3.1 Unity：从点击到上报

1. **识别点击** — [ArTapPlacer.cs](../unity/Assets/InspectionAR/Scripts/AR/ArTapPlacer.cs) `Update`
   - 只在按下那一帧响应：真机用 `Touchscreen`，编辑器用 `Mouse`。
   - `IsBlockedByUi` 依次判断：软键盘是否可见；输入框是否正在编辑；用 `EventSystem.RaycastAll` 检查触点下有没有 UI。任何一项为真就忽略这次点击，这对应任务书「点按钮或输入框不误放标记」。
   - `ARRaycastManager.Raycast(..., PlaneWithinPolygon)`：只接受落在已识别平面多边形内部的点。取最近的、处于跟踪状态的水平面，触发 `PlaneTapped(plane, pose)`。
2. **放置标记** — [InspectionAppController.cs](../unity/Assets/InspectionAR/Scripts/InspectionAppController.cs) `OnPlaneTapped`
   - 跟踪连续稳定 1.5 秒后才允许放置，避免在定位还不可靠时创建锚点。
   - `ARAnchorManager.AttachAnchor(plane, pose)` 把锚点附着在平面上，标记作为锚点的子物体。ARCore 修正空间位置时，锚点和标记一起调整。
   - 同一时间只有一个待提交标记，再点一次相当于移动标记。
3. **跟踪丢失时的保护** — [IssueMarker.cs](../unity/Assets/InspectionAR/Scripts/AR/IssueMarker.cs) `HasTracking`
   - 锚点或会话不在 `Tracking` 状态时隐藏标记、拒绝提交，恢复后再显示，避免把漂移中的位置上报出去。
4. **提交** — `Submit` → [IssueApiClient.cs](../unity/Assets/InspectionAR/Scripts/Net/IssueApiClient.cs) `Send<T>`
   - 用 `UnityWebRequest` 协程发 JSON，超时 8 秒，回调在主线程执行，可以直接更新 UI。
   - 先解析再调用回调，界面代码出错不会被误报成「响应解析失败」。
   - `DescribeError` 区分两类错误：后端返回 4xx/5xx 时显示后端的错误信息；连不上时提示检查地址、Wi-Fi 和防火墙。
   - 失败时保留标记和表单内容，可以直接重试。
5. **后端地址** — [BackendSettings.cs](../unity/Assets/InspectionAR/Scripts/Net/BackendSettings.cs)：保存在 PlayerPrefs，自动补全 `http://`。
6. **界面** — [IssueReportUI.cs](../unity/Assets/InspectionAR/Scripts/UI/IssueReportUI.cs) 和 [UiFactory.cs](../unity/Assets/InspectionAR/Scripts/UI/UiFactory.cs)：在 `Awake` 中用代码搭建 UGUI，只负责显示和收集输入，不含业务逻辑。
7. **编辑器工具**
   - [InspectionProjectSetup.cs](../unity/Assets/InspectionAR/Editor/InspectionProjectSetup.cs)：两个菜单，分别一键配置 Android 构建参数、生成场景和预制体，避免手工连线出错。
   - [AndroidBuildCompatibility.cs](../unity/Assets/InspectionAR/Editor/AndroidBuildCompatibility.cs)：构建时向 `gradle.properties` 追加 `android.uniquePackageNames=false`，解决 ARCore 6.0.x 两个 AAR 命名空间相同、与 AGP 9 冲突的问题。

**坐标含义**：上报的 `x/y/z` 是本次 AR 会话坐标系下的位置（米），原点是 App 启动时手机的位置，App 重启后原点会变（任务书不要求恢复）。

### 3.2 Go：请求处理与持久化

- [server.go](../backend/internal/api/server.go) `NewHandler`
  - 用 Go 1.22 的 `"METHOD /path/{id}"` 路由注册 5 个接口，外面套 CORS 和访问日志中间件。
  - 错误统一为 `{"error":{"code","message","field"}}`：校验错误返回 400，找不到 ID 返回 404，其他错误返回 500。
- [issue.go](../backend/internal/issue/issue.go) `Normalize`
  - 去掉首尾空格，校验标题、长度、优先级枚举、坐标是否存在。
  - 文本中出现 U+FFFD 时拒绝：这表示客户端没有按 UTF-8 发送。
  - 坐标保留 3 位小数。
- [store.go](../backend/internal/issue/store.go)
  - 全部数据放在内存切片中，用 `RWMutex` 保护并发读写。
  - 写操作在副本上修改，`save` 写盘成功后才替换内存数据。
  - `save` 先写 `.tmp` 再 `os.Rename`，保证磁盘上永远是完整的文件。
  - 数据文件损坏时 `OpenStore` 拒绝启动，避免下次写入把旧数据覆盖成空列表。
- 为什么选 JSON 文件而不是 SQLite：任务书允许两者；JSON 不需要 cgo 和第三方依赖，数据量在原型级别，整文件重写足够简单可靠。

### 3.3 React：加载与修改状态

界面使用 shadcn/ui（Base UI 版本，Tailwind CSS v4）。组件源码在 `web/src/components/ui/`，由 shadcn CLI 生成，归项目所有，可以直接修改。数据逻辑和界面分开：

- [useIssues.ts](../web/src/hooks/useIssues.ts) — 数据层，界面只读取状态并调用 `reload` / `changeStatus`
  - `reload`：每 5 秒轮询一次。用 `requestSeq` 只采用最后一次请求的结果，避免慢请求覆盖新数据；修改状态期间发出的轮询可能读到旧状态，用 `mutationVersion` 丢弃这些结果。出现新问题时高亮 8 秒，并弹出通知。
  - `changeStatus`：调 PATCH，成功后用后端返回的数据替换这一行，成功或失败都用 sonner 提示。下拉框的值绑定在 `issue.status` 上，请求失败时会自动保持原值。
- [api.ts](../web/src/api.ts)
  - 请求 8 秒超时。
  - 优先显示后端的错误信息；Vite 代理连不上后端时返回没有错误体的 5xx，这时提示「后端服务不可用」。
- [App.tsx](../web/src/App.tsx) — 页面组合
  - 筛选（状态、优先级、关键词）和排序都在前端用 `useMemo` 计算。按优先级排序时依赖稳定排序，同级保持「最新在前」。
  - 详情抽屉只保存选中问题的 ID，每次从最新列表中取数据，所以轮询或修改状态后抽屉内容会自动同步。
- 其他组件
  - [IssueTable.tsx](../web/src/components/IssueTable.tsx)：点击行打开详情。状态下拉框所在单元格阻止点击和键盘事件冒泡，避免改状态时同时打开详情。窄屏下优先级显示在标题下方。
  - [IssueDetailSheet.tsx](../web/src/components/IssueDetailSheet.tsx)：完整描述、xyz 坐标、ID 复制、一键切换状态。
  - [theme.ts](../web/src/lib/theme.ts)：明暗主题，跟随系统或手动切换。没有用 next-themes：它会往 React 树里插 `<script>`，React 19 会报警。

### 3.4 关于定位偏移的取舍

真机复测时，转开相机再转回来，标记有几厘米偏移，日志中出现多次 `VIO_OUTPUT_NOT_TRACKING`（跟踪丢失）。处理过程如下：

1. 第一版：直接在标记上挂 `ARAnchor`。
2. 第二版：改为附着在平面上的锚点，并增加 1.5 秒稳定期和丢失跟踪时的保护。偏移仍有几厘米。
3. 尝试过「固定参考图校准」：识别一张打印图片，再按它修正标记位置。评估后撤回，恢复第 2 种。<!-- 补充撤回原因 -->

当前版本是第 2 种。剩余偏移在 [README.md](../README.md) 的已知问题中如实记录。如果以后需要跨会话或更高精度，可以考虑 Cloud Anchors 或图像标记。

## 4. 演示视频录制脚本

两段视频可以同时录：手机用系统自带录屏，电脑用 OBS 或 Xbox Game Bar（Win+G）。这样两段的时间线能对上。

### 视频一：手机（3～5 分钟）

| 步骤 | 操作 | 要拍到的内容 |
|---|---|---|
| 1 | 打开「Inspection AR」，缓慢扫描桌面或地面 | 顶部状态从「正在识别水平面」变为「已识别 N 个水平面」，出现青色平面 |
| 2 | 在顶部填写后端地址，点「测试连接」 | 「后端连接正常」 |
| 3 | 点「测试连接」按钮和输入框，再点表单空白处 | 都没有放出标记（任务书 3.1-6） |
| 4 | 点击平面，再点另一处 | 黄色闪动标记出现并移动，表单显示 x/y/z |
| 5 | 不填标题直接提交 | 「请填写标题（必填）」 |
| 6 | 填写标题、描述，选「高」优先级后提交 | 绿色「提交成功」和 ID，标记变红 |
| 7 | 再放一个标记，选「低」优先级提交 | 标记变绿 |
| 8 | （演示失败提示）电脑上按 Ctrl+C 停掉 Go，再提交一条 | 红色「提交失败：无法连接后端…」，表单仍在；重启 Go 后再提交成功 |
| 9 | 转开相机再转回来 | 标记仍在原位置附近（口头说明已知的几厘米偏移） |

### 视频二：电脑（3～5 分钟）

| 步骤 | 操作 | 要拍到的内容 |
|---|---|---|
| 1 | `cd backend && go run .` | 日志中的数据文件路径、「已加载 N 条」、局域网地址 |
| 2 | `cd web && npm run dev`，浏览器打开 `localhost:5173` | 问题列表、状态统计、「后端在线」 |
| 3 | 手机提交一条问题 | 5 秒内自动出现带「新」标签的记录；Go 日志出现来自手机 IP 的 `POST /api/issues 201` |
| 4 | 把状态依次改为「处理中」「已解决」 | 成功提示，更新时间变化 |
| 5 | 在终端按 Ctrl+C 停掉 Go | 页面出现「后端连接中断」错误条，不白屏 |
| 6 | 再次 `go run .` | 日志显示「已加载 N 条」；页面自动恢复，状态仍是修改后的值（重启后数据仍在） |
| 7 | （可选）`go test ./...` | 测试全部通过 |

## 5. 提交前检查

- [ ] 所有改动已提交；推送到 GitHub / Gitee 时保留完整 Git 历史。
- [ ] `Library/`、`Builds/`、`Logs/`、`node_modules/`、`backend/data/` 没有入库（已在 `.gitignore` 中）。
- [ ] `AI_LOG.md` 补到最后一条 Prompt，并填写每条的「采用 / 修改后采用 / 未采用」。
- [ ] `README.md` 的已知问题和视频中看到的情况一致。
- [ ] 没有提交 API Key、Token、账号或隐私信息；AI 对话分享链接已脱敏。
- [ ] 视频已上传，链接在评审期间可访问；如果有提取码或有效期，写在提交说明里。
- [ ] （可选）附带 APK。
