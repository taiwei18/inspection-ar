# AI 使用记录

按实际发送顺序记录。用到两个工具：

- **Claude Code**（桌面版）：P-001～P-004、P-011
- **OpenAI Codex**（ChatGPT）：P-005～P-010，其「结果」根据 Codex 的回复和当时提交的 README 整理

AI 在执行过程中提问、我作答的内容不是主动发送的 Prompt，放在对应条目的「结果」中注明。

## P-001

- 工具：Claude Code
- 目标：根据任务书规划并搭建三端项目（Go 后端、React 管理端、Unity AR 客户端）的第一版

### Prompt 原文

> @"C:\Users\Administrator\Downloads\Unity-AR研发实操题-候选人任务书-简略版.md"
> 规划构建项目

### 结果

- 修改后采用：三端代码作为第一版保留；Unity 部分后来在真机验证中修正（见 P-006～P-008）。
- AI 生成了三端代码、README 和本文件。过程中 AI 问本机没有 Go 时怎么处理，我选择「用 winget 安装」（安装了 Go 1.27.0）。
- 验证：
  - Go：`go vet ./...`、`go test ./...` 通过，覆盖持久化后重新加载，标题、优先级、状态、坐标校验，404，CORS 预检；
  - React：`npm run build`（含 `tsc` 类型检查）通过；
  - 联调：启动 Go，用与 Unity `JsonUtility` 格式一致的请求体模拟上报 3 条问题，浏览器中看到列表并把两条改为「处理中」「已解决」；停掉 Go 后页面显示「无法加载问题列表」，没有白屏；重启 Go 后 3 条数据和修改后的状态都还在；
  - Unity：当时本机没有 Unity，只对 Unity API 桩代码做了 C# 编译检查。
- AI 联调时自己发现并修正了两处：
  1. Windows 命令行中的中文按 GBK 发送时，后端会把乱码存进去 → 增加 UTF-8 校验，返回 400，并补了测试；
  2. 启动日志列出的局域网 IP 包含虚拟网卡地址，容易填错 → 地址后面标注网卡名。

## P-002

- 工具：Claude Code
- 目标：建立 Git 仓库，保留开发历史

### Prompt 原文

> 初始化 Git 仓库，并把当前代码提交为第一个 commit

### 结果

- 采用：在 `main` 分支创建第一个提交 `0f19359`。
- 验证：`git log` 显示该提交，`git status` 无未提交文件；确认 `node_modules`、`dist`、数据目录没有入库。

## P-003

- 工具：Claude Code
- 目标：把项目从临时目录迁到固定位置

### Prompt 原文

> 项目迁到D:\code下

### 结果

- 采用：AI 在 `D:\code` 下新建了 `inspection-ar` 子文件夹，没有直接放进根目录，以免和其他项目混在一起。

## P-004

- 工具：Claude Code
- 目标：确认迁移结果

### Prompt 原文

> 现在呢

### 结果

- 采用：AI 发现迁移时没有复制 `.git`、`.claude/launch.json` 和 `web/node_modules`，于是复制回 Git 历史、从仓库还原 `launch.json`，并用 `npm ci` 重新安装依赖。
- 验证：新位置 `git log` 仍是 `0f19359`，工作区干净；`go test ./...`、`npm run build` 通过。

## P-005

- 工具：Codex
- 目标：弄清楚 Unity 实测具体要做什么，以及 AI 能否代为完成

### Prompt 原文

> 在 Unity 6000.0 LTS 里打开 unity/，按 README.md 第 3 节操作：先执行菜单「Inspection AR → 1. 配置 Android 构建参数」，再到 XR 设置里勾选 ARCore，然后执行「Inspection AR → 2. 生成 AR 场景」，最后打包到手机。Unity 包版本（AR Foundation 6.0.3 等）和 XR Origin 的自动创建菜单都是凭经验写的，第一次打开最可能在这里出问题，报错发给我就行。验证通过后，记得更新 README「已知问题」里的第一条。 什么意思你能做吗

### 结果

- 采用（只有解释，没有改代码）：AI 说明需要在编辑器和真机上实测；它检查了本机，没有找到 Unity Hub、Unity 编辑器和 adb，所以暂时无法实测。

## P-006

- 工具：Codex
- 目标：安装 Unity 环境，完成编辑器内的验证

### Prompt 原文

> 你安装吧都

### 结果

- 采用：AI 在 `D:\Unity` 安装了 Unity Hub、Unity 6000.0.85f1 LTS 和 Android Build Support（SDK、NDK、OpenJDK），启用 ARCore 和新输入系统，生成了 AR 场景，并更新了 README 中的版本和验证结果。
- 首次导入时编辑器自动把依赖调整为 AR Foundation / ARCore XR Plugin 6.0.7、Input System 1.20.0，与第一版的预设不同，README 已按实际版本更新。
- 验证：Codex 报告 Android 平台编译检查通过。当时没有连手机，未打包 APK。

## P-007

- 工具：Codex
- 目标：在手机和管理端上真正看到运行效果

### Prompt 原文

> 手机上怎么看呢？还有管理端怎么看到？怎么没有启动

### 结果

- 采用：AI 构建 APK 并安装到 vivo X200s（Android 15），补装了 Google Play Services for AR，启动了 Go 后端和 React 管理端。过程中还修了两个问题：
  - 场景生成时标记预制体是空引用；
  - ARCore 6.0.7 与 AGP 9 的 AAR 命名空间冲突，新增 `AndroidBuildCompatibility.cs` 自动添加兼容配置。
- 过程中回答 AI 的提问：「已连接并允许 USB 调试」「已看到相机画面，正在识别平面」。
- 验证：我在手机上提交了「袜子」「电脑」「衣服」三条问题，在 `http://localhost:5173` 上看到了这三条（使用 `adb reverse` 加 `http://127.0.0.1:8080` 连接后端）。

## P-008

- 工具：Codex
- 目标：解决转开相机再转回来后标记位置偏移的问题

### Prompt 原文

> 现在都可以正常运转，但是标记后，摄像头转到其他地方再转回来，位置跟原来标记的时候就不太对了。这个怎么解决呢

### 结果

- 修改后采用（问题没有完全解决）：AI 把标记改为附着在平面上的锚点（`ARAnchorManager.AttachAnchor`），增加 1.5 秒跟踪稳定期，跟踪丢失时隐藏标记并禁止提交。这一版保留为最终版本。
- 我的检查：真机复测后反馈「仍有明显偏移」，第二次复测回答「几厘米，仍在原来附近」。日志显示锚点创建成功，但多次出现 `VIO_OUTPUT_NOT_TRACKING`。剩余偏移已记录在 README 的已知问题中。

## P-009

- 工具：Codex
- 目标：按 AI 的建议尝试「固定参考图校准」，进一步减小偏移

### Prompt 原文

> 可以 按照这种做吧

### 结果

- 未采用（在 P-010 中撤回）：AI 新增了 `ReferenceCalibration.cs`、参考图打印页面和场景配置，实现「识别打印图 → 校正标记位置」。日志确认手机能识别参考图、校准成功，但用打印件实测偏移是否改善还没做就撤回了。
- 放弃原因：<!-- 补充你的考虑 -->

## P-010

- 工具：Codex
- 目标：放弃参考图方案，回到 P-008 的版本，并对照任务书补全文档

### Prompt 原文

> 取消参考图做法吧 恢复上一版本 并@根据这个文件补全一下文档

（`@` 后附带的文件名在分享页面中看不到。）

### 结果

- 部分完成：AI 删除了参考图相关代码，恢复到 P-008 的版本，README 中注明参考图方案已撤回；但 README 链接的 `docs/DELIVERY.md` 没有创建，本文件也没有更新。剩余部分在 P-011 中补完。

## P-011

- 工具：Claude Code
- 目标：接着完成 P-010 没做完的部分

### Prompt 原文

> [REDACTED: ChatGPT 对话分享链接] 继续未完成的事情

### 结果

- 采用：AI 读取了分享的 Codex 对话，确认参考图代码已经全部撤回；把 P-010 中的「这个文件」理解为任务书，对照任务书新建了 [docs/DELIVERY.md](docs/DELIVERY.md)（逐项验收、核心代码说明、录屏脚本、提交前检查），补全了本文件 P-002～P-011，并修正了 README。
- 验证：用 Unity 6000.0.85f1 以命令行模式按 Android 目标重新编译，`Assembly-CSharp` 和 `Assembly-CSharp-Editor` 都没有报错；全局搜索确认项目中已没有参考图或校准相关代码。
- AI 自查：它起初在 DELIVERY.md 里自行写了撤回参考图的原因，这个原因我并没有说过，AI 发现后删除，改为留给我补充。
