# 交付说明：现场巡检问题标注

您好，这是「Unity-AR 研发实操题：现场巡检问题标注」的交付材料。全部内容都在这个仓库里：

- 仓库地址：<https://github.com/taiwei18/inspection-ar>（保留完整 Git 历史）
- 下载方式：`git clone`，或在 GitHub 页面点 Code → Download ZIP

```text
Unity AR (Android 真机) ──POST /api/issues──▶ Go 后端 (JSON 文件持久化) ◀──GET / PATCH── React 管理端 (浏览器)
```

## 交付内容

| 内容 | 位置 | 说明 |
|---|---|---|
| Unity 客户端源码 | [`unity/`](unity/) | Unity 6000.0 LTS + AR Foundation / ARCore，Android |
| React 管理端源码 | [`web/`](web/) | React + TypeScript + Vite，shadcn/ui |
| Go 后端源码 | [`backend/`](backend/) | 只用标准库，数据保存在 JSON 文件；含单元测试 |
| 演示视频 | [`videos/demo-1.mp4`](videos/demo-1.mp4)（1 分 28 秒）<br>[`videos/demo-2.mp4`](videos/demo-2.mp4)（2 分 06 秒） | 手机录制，下面单独说明 |
| 说明文档 | [`README.md`](README.md) | 版本、启动方式、真机配置后端地址、接口说明、完成情况和已知问题 |
| AI 使用记录 | [`AI_LOG.md`](AI_LOG.md) | 按顺序记录发给 AI 的 Prompt 和处理结果 |
| 验收与代码说明 | [`docs/DELIVERY.md`](docs/DELIVERY.md) | 对照任务书逐项验收，核心代码讲解，定位偏移问题的处理过程 |
| 部署脚本 | [`deploy/`](deploy/) | 部署到 Linux 服务器（systemd + nginx） |

## 演示视频

两段视频都在 [`videos/`](videos/) 目录，在 GitHub 页面点开文件即可在线播放，也可以下载后观看。合起来展示了任务书要求的内容：

- 真机识别平面并放置标记；
- 手机提交问题；
- React 管理端看到该问题，并修改状态；
- Go 服务重启后数据仍然存在。

## 在线体验（可选）

后端和管理端已部署到公网服务器，不需要在本地启动：

- 管理端：<http://117.72.117.250/>
- 手机 App：在顶部输入框填 `117.72.117.250`，点「测试」，显示「后端连接正常」就可以使用了，Wi-Fi 或 4G 都行。

这个演示环境没有登录鉴权，只放了演示数据。如果评审期间无法访问，请按下一节在本地启动。

## 本地启动

完整步骤见 [README.md](README.md)，这里是最短路径。

**1. Go 后端**（需要 Go 1.22 及以上）

```bash
cd backend
go run .
```

- 监听 `:8080`，数据保存在 `backend/data/issues.json`；
- 启动日志会打印手机可以用的局域网地址；
- 运行测试：`go test ./...`。

**2. React 管理端**（需要 Node.js 22）

```bash
cd web
npm install
npm run dev
```

浏览器打开 <http://localhost:5173>。

**3. Unity 客户端**（Unity 6000.0 LTS，需要 Android Build Support，以及支持 ARCore 的 Android 手机）

1. Unity Hub 打开 `unity/` 目录；
2. 执行菜单 Inspection AR → 1. 配置 Android 构建参数；
3. Project Settings → XR Plug-in Management → Android 页签，勾选 ARCore；
4. 执行菜单 Inspection AR → 2. 生成 AR 场景；
5. 执行菜单 Inspection AR → 3. 构建 APK，或者 Build And Run。

在 Windows 上构建遇到 Java 的 loopback 错误时，处理方法见 README 的「3. Unity 客户端」。

**手机连接后端**：在 App 顶部输入框填写后端地址，点「测试」。三种方式任选：

- 电脑的局域网 IP，例如 `192.168.0.102:8080`，Go 启动日志里有；
- USB 转发：先执行 `adb reverse tcp:8080 tcp:8080`，地址填 `127.0.0.1:8080`；
- 公网演示环境：`117.72.117.250`。

## 完成情况概览

- **必做功能**：三端全部完成，形成「手机上报 → Go 保存 → 网页查看 → 网页修改状态」的闭环。Go 重启后数据保留。手机在真机上运行，识别真实水平面，标记固定在空间位置，属于真实 AR。
- **扩展功能**：
  - 管理端：筛选、搜索、按优先级排序、详情抽屉、明暗主题、5 秒自动刷新，有新问题时弹出通知；
  - 手机端：
    - 标记样式随优先级和状态变化；网页修改状态后，手机上的标记随之更新；
    - 历史问题列表，可以在手机上修改状态、在 AR 中查找标记；
    - 定位质量提示和多角度观察引导，用来减少转开再回来的偏移；
  - 后端：单元测试，一键部署到公网服务器。
- **未完成和已知问题**：
  - 登录鉴权、跨会话 / 跨设备定位都没有做；
  - 转开再回来，标记仍可能有几厘米偏差；
  - 详细说明见 [README.md](README.md) 的「完成情况」和「已知问题与限制」。
