# EasyNaive 设计文档

## 0. 实施状态总览

本文档已按当前仓库实现状态更新，状态以 `src/` 下实际代码为准，更新日期为 `2026-04-21`。

状态说明:

- `已确认`: 设计结论已确定，这一节本身不是待开发功能
- `已完成`: 设计范围对应功能已基本落地
- `部分完成`: 已有可运行实现，但仍有明显缺口
- `未完成`: 文档中规划了，但仓库里还没有真正实现

当前总览:

- `M1 基础骨架`: 已完成
- `M2 代理模式 MVP`: 已完成
- `M3 自动选节点`: 部分完成，节点测速和 `manual/auto` 已有，自动模式下真实出口状态仍未闭环
- `M4 TUN 模式`: 部分完成，TUN 启停、提权 helper、DNS 劫持与 TCP 主链路已落地，但 `naive` 下仍属于兼容型 TUN，完整原生 UDP 能力未覆盖
- `M5 稳定性与安装`: 部分完成，单实例、开机自启、`sing-box check`、应用日志、自检、启动恢复已完成，但安装打包、升级迁移和测试工程仍缺失

主要已完成项:

- WinForms 托盘主程序、多节点管理、订阅导入、文本导入
- `sing-box` 配置生成、`selector/urltest`、`rule/global/direct`
- `Clash API` 热切换、延迟测试、基础运行状态、上下行速率显示
- 单实例、开机自启、`sing-box check`、`app.log`、自检、启动恢复
- `EasyNaive.Elevation` helper、TUN 启停、TUN 下 DNS 劫持与兼容型联网验证

主要未完成项:

- 安装打包、WiX、卸载与升级迁移
- `tests/` 测试工程与自动化验证
- `EasyNaive.Platform.Windows` 分层拆分尚未落地
- 敏感数据保护、权限收敛和 service 化仍未完成
- `naive` 仅支持兼容型 TUN，完整原生 UDP TUN 方案尚未设计完成

## 1. 文档信息【已更新】

- 项目名称: EasyNaive
- 目标平台: Windows
- 文档状态: Draft v1 / 实施状态更新于 2026-04-21
- 面向对象: 项目维护者、实现者、测试者
- 文档范围: Windows 客户端、代理内核集成、配置生成、节点管理、分流、TUN、托盘、权限与部署

## 2. 背景【已确认】

EasyNaive 是一个基于 `sing-box` 的 Windows 端代理工具，核心代理协议以 `naive` 为主，目标是提供一个面向日常使用的图形化客户端。与直接运行命令行代理不同，本项目需要解决以下桌面产品问题:

- 多节点管理与切换
- 智能分流、中国大陆直连、外部代理
- 一键切换为全局代理或全部直连
- 同时支持系统代理模式与 TUN 模式
- 常驻 Windows 右下角托盘，便于快速操作
- 稳定的本地状态持久化、日志、错误恢复与升级

项目不自行重写代理协议栈，而是把 `sing-box` 当作数据面内核，把 Windows 客户端实现为控制面与用户界面。

## 3. 设计目标【部分完成】

### 3.1 功能目标

- 支持多个 `naive` 节点的增删改查
- 支持手动选节点和自动选优
- 支持三种路由模式:
  - 智能分流
  - 全局代理
  - 全部直连
- 支持两种接管模式:
  - 代理模式
  - TUN 模式
- 支持托盘常驻、开机启动、快速切换
- 支持运行日志、内核日志、基础自检
- 支持配置持久化和上次运行状态恢复

### 3.2 非目标

- 不自行实现代理内核
- 不在第一阶段支持跨平台
- 不在第一阶段实现复杂订阅生态兼容
- 不在第一阶段实现内嵌浏览器 UI 或 Web 前端壳

## 4. 总体方案【部分完成】

系统按控制面与数据面分层:

```text
+-----------------------+
| EasyNaiveTray.exe     |
| 托盘 + 窗口 + 控制逻辑 |
+-----------+-----------+
            |
            | IPC / Process / HTTP
            v
+-----------------------+
| sing-box.exe          |
| mixed / tun / route   |
| dns / selector / api  |
+-----------+-----------+
            |
            v
+-----------------------+
| naive outbound        |
| via cronet/libcronet  |
+-----------------------+
```

### 4.1 控制面

控制面由 Windows 客户端负责:

- 用户界面
- 托盘交互
- 节点库与设置管理
- `sing-box` 配置生成
- `sing-box` 进程管理
- 模式切换与状态恢复
- TUN 所需提权协调

### 4.2 数据面

数据面完全交给 `sing-box`:

- 本地 `mixed` 入站
- 本地 `tun` 入站
- `naive` 出站
- `selector`/`urltest` 组
- 路由与 DNS 分流
- Clash API 控制接口

## 5. 技术栈【已确认】

### 5.1 客户端语言

Windows 客户端使用 `C# + .NET 8`。

选择理由:

- Windows 桌面系统集成成熟
- `NotifyIcon` 和托盘菜单实现成本低
- 开机启动、进程控制、命名管道、服务通信都容易落地
- 适合工具型桌面应用，不需要额外浏览器运行时

### 5.2 UI 技术

首选 `WinForms`。

原因:

- 托盘常驻场景比复杂富界面更重要
- 启动快，依赖少
- Windows 原生交互实现直接
- 适合“主窗口 + 托盘菜单 + 设置页”的工具型产品

### 5.3 代理内核

- 现有 `bin/sing-box/sing-box.exe`
- 现有 `bin/sing-box/libcronet.dll`
- `naive` 出站由 `sing-box` 内部 `with_naive_outbound` 能力提供

### 5.4 提权模型

TUN 相关能力使用单独 helper 或 Windows service 承担，避免将主托盘程序长期运行在管理员权限下。

## 6. 总体架构【部分完成】

### 6.1 进程组成

运行时进程包括:

- `EasyNaiveTray.exe`
  - 主 UI 进程
  - 托盘常驻
  - 普通用户权限启动
- `sing-box.exe`
  - 实际代理内核
  - 由主程序管理生命周期
- `EasyNaive.Elevation.exe` 或 `EasyNaiveService`
  - 仅在 TUN 模式需要
  - 负责管理员权限范围内的操作

### 6.2 分层

```text
UI Layer
  -> Tray, Forms, ViewModels

Application Layer
  -> CoreController, UseCases, StateMachine

Integration Layer
  -> ConfigBuilder, SingBoxProcessManager, ClashApiClient

Platform Layer
  -> WindowsTray, StartupRegistry, Privilege, Shell

Persistence Layer
  -> JsonStore, Cache Paths, Logs
```

## 7. 仓库目录设计【部分完成】

```text
EasyNaive/
  AGENTS.md
  EasyNaive.sln

  src/
    EasyNaive.App/
      Program.cs
      Tray/
      Forms/
      Resources/
      Assets/

    EasyNaive.Core/
      Models/
      Enums/
      Services/
      State/
      Validation/

    EasyNaive.SingBox/
      Config/
      ClashApi/
      Process/
      Health/
      Rules/

    EasyNaive.Platform.Windows/
      Tray/
      Startup/
      Shell/
      Proxy/
      Privilege/
      Logging/

    EasyNaive.Elevation/
      Ipc/
      Commands/
      Service/

  tests/
    EasyNaive.Core.Tests/
    EasyNaive.SingBox.Tests/
    EasyNaive.Platform.Windows.Tests/

  docs/
    design.md

  assets/
    icons/
    rules/
    templates/

  scripts/
    build-app.ps1
    build-singbox.ps1
    package.ps1

  packaging/
    wix/

  patches/
    sing-box/

  sources/
    sing-box/
    naiveproxy/

  bin/
    sing-box/
    naiveproxy/

  artifacts/
```

### 7.1 目录约束

- `src/` 仅放本项目代码
- `sources/` 仅放上游源码
- 根目录 `bin/` 仅放运行依赖，不作为 .NET 构建输出目录
- `artifacts/` 放本项目编译产物
- `patches/` 记录对上游的维护性补丁

## 8. 运行时目录设计【部分完成】

### 8.1 安装目录

```text
%ProgramFiles%\EasyNaive\
  EasyNaiveTray.exe
  EasyNaive.Elevation.exe
  sing-box\
    sing-box.exe
    libcronet.dll
  assets\
    icons\
```

### 8.2 用户数据目录

```text
%LocalAppData%\EasyNaive\
  data\
    nodes.json
    subscriptions.json
  config\
    active.json
  state\
    app-state.json
  cache\
    cache.db
  logs\
    app.log
    sing-box.log
  rules\
    cn-domain.srs
    cn-ip.srs
```

### 8.3 设计原则

- 程序升级不覆盖用户数据
- 日志、缓存、规则集分目录存放
- `sing-box` 活动配置单独输出，方便诊断
- 所有可恢复状态进入 `state/` 或 `cache/`

## 9. 核心业务模型【部分完成】

### 9.1 Node

每个节点对应一个 `naive` outbound。

建议字段:

| 字段 | 类型 | 说明 |
|---|---|---|
| `Id` | string | 稳定唯一标识 |
| `Name` | string | 用户可见名称 |
| `Group` | string | 节点分组 |
| `Server` | string | 服务器域名或地址 |
| `ServerPort` | int | 服务器端口 |
| `Username` | string | 认证用户名 |
| `Password` | string | 认证密码 |
| `TlsServerName` | string | TLS SNI |
| `UseQuic` | bool | 是否启用 QUIC |
| `UseUdpOverTcp` | bool | 是否启用 UDP over TCP |
| `ExtraHeaders` | map | 可选请求头 |
| `Enabled` | bool | 是否启用 |
| `SortOrder` | int | 排序 |
| `Remark` | string | 备注 |

### 9.2 AppSettings

| 字段 | 类型 | 说明 |
|---|---|---|
| `CaptureMode` | enum | `Proxy` / `Tun` |
| `RouteMode` | enum | `Rule` / `Global` / `Direct` |
| `NodeMode` | enum | `Manual` / `Auto` |
| `SelectedNodeId` | string | 手动模式选中的节点 |
| `ProxyMixedPort` | int | 本地 mixed 监听端口 |
| `ClashApiPort` | int | 本地 Clash API 端口 |
| `ClashApiSecret` | string | API 密钥 |
| `EnableAutoStart` | bool | 开机自启 |
| `EnableMinimizeToTray` | bool | 关闭窗口隐藏到托盘 |
| `EnableTUNStrictRoute` | bool | TUN 严格路由 |
| `LogLevel` | enum | 日志级别 |

### 9.3 RuntimeState

| 字段 | 类型 | 说明 |
|---|---|---|
| `CoreStatus` | enum | `Stopped` / `Starting` / `Running` / `Stopping` / `Error` |
| `CurrentProfileHash` | string | 当前配置签名 |
| `LastError` | string | 最近错误 |
| `CurrentRealNodeId` | string | 当前实际出口节点 |
| `CurrentLatency` | int? | 当前延迟 |
| `LastStartTime` | datetime | 最近启动时间 |

## 10. 功能设计【部分完成】

### 10.1 节点管理

客户端负责节点库维护。

支持能力:

- 新增节点
- 编辑节点
- 删除节点
- 启用/禁用节点
- 排序与分组
- 导出与导入

数据持久化至 `nodes.json`。

### 10.2 手动与自动选节点

每个节点生成一个独立 `naive` outbound，例如:

- `node-hk-01`
- `node-jp-01`
- `node-us-01`

在此之上生成:

- `manual`: `selector` 出站组
- `auto`: `urltest` 出站组
- `proxy`: 二级 `selector`，成员为 `manual` 与 `auto`

`proxy` 作为所有代理流量的统一出口。

### 10.3 路由模式

路由模式使用 `Clash API mode + clash_mode 规则项` 表达。

#### 智能分流

- 中国大陆域名与 IP 直连
- 局域网与私网直连
- 其他流量走 `proxy`

#### 全局代理

- 所有流量走 `proxy`
- 仅必要系统例外保持直连

#### 全部直连

- 所有流量走 `direct`
- 内核仍可存活，便于快速切回

### 10.4 接管模式

#### 代理模式

使用 `mixed` 入站:

- 本地 `SOCKS4/SOCKS5/HTTP`
- `set_system_proxy = true`
- 适用于遵循系统代理或手动代理的程序

特点:

- 不需要完整系统级网络接管
- 风险低
- 适合作为第一阶段默认模式

#### TUN 模式

使用 `tun` 入站:

- `auto_route = true`
- Windows 下建议 `strict_route = true`
- 配合 `route.auto_detect_interface = true`

特点:

- 可接管不走系统代理的程序
- 更接近“全局接管”
- 对权限、稳定性和系统兼容性要求更高

### 10.5 托盘常驻

应用关闭主窗口时不退出，只隐藏到托盘。

托盘菜单建议包含:

- 连接
- 断开
- 接管方式
- 路由模式
- 节点选择
- 自动选择开关
- 打开主窗口
- 查看日志
- 重启内核
- 开机启动
- 退出

### 10.6 启动与恢复

启动时流程:

1. 加载节点与设置
2. 恢复上次 `CaptureMode`、`RouteMode`、`NodeMode`
3. 生成活动配置
4. 启动 `sing-box`
5. 若开启系统代理模式则应用系统代理
6. 刷新托盘状态

## 11. sing-box 配置生成设计【部分完成】

### 11.1 配置生成原则

- 配置完全由程序生成，不要求用户手工编辑
- 所有运行态配置输出到 `config/active.json`
- 配置生成结果必须可被 `sing-box check` 验证
- 切换节点或模式时尽量复用热更新控制，不频繁重启

### 11.2 入站设计

#### 代理模式

生成 `mixed` 入站:

```json
{
  "type": "mixed",
  "tag": "mixed-in",
  "listen": "127.0.0.1",
  "listen_port": 2080,
  "set_system_proxy": true
}
```

#### TUN 模式

生成 `tun` 入站:

```json
{
  "type": "tun",
  "tag": "tun-in",
  "auto_route": true,
  "strict_route": true,
  "stack": "system"
}
```

最终是否选择 `system`、`mixed` 或 `gvisor` 栈由实现阶段做实测后决定，默认先从 `system` 开始。

### 11.3 出站设计

固定包含:

- 多个 `naive` 节点出站
- `manual` 选择组
- `auto` 测速组
- `proxy` 总代理出口
- `direct`
- `block`

样例:

```json
{
  "outbounds": [
    { "type": "naive", "tag": "node-hk-01" },
    { "type": "naive", "tag": "node-jp-01" },
    {
      "type": "selector",
      "tag": "manual",
      "outbounds": ["node-hk-01", "node-jp-01"],
      "default": "node-hk-01"
    },
    {
      "type": "urltest",
      "tag": "auto",
      "outbounds": ["node-hk-01", "node-jp-01"],
      "interval": "3m",
      "tolerance": 50
    },
    {
      "type": "selector",
      "tag": "proxy",
      "outbounds": ["manual", "auto"],
      "default": "manual"
    },
    { "type": "direct", "tag": "direct" },
    { "type": "block", "tag": "block" }
  ]
}
```

### 11.4 路由规则设计

路由统一通过 `proxy` 和 `direct` 两个出口表达。

基础规则顺序:

1. `clash_mode = direct` -> `direct`
2. `clash_mode = global` -> `proxy`
3. 私网 IP -> `direct`
4. 中国大陆域名规则集 -> `direct`
5. 中国大陆 IP 规则集 -> `direct`
6. 兜底 `final = proxy`

样例:

```json
{
  "route": {
    "auto_detect_interface": true,
    "rules": [
      { "clash_mode": "direct", "action": "route", "outbound": "direct" },
      { "clash_mode": "global", "action": "route", "outbound": "proxy" },
      { "ip_is_private": true, "action": "route", "outbound": "direct" },
      { "rule_set": ["cn-domain"], "action": "route", "outbound": "direct" },
      { "rule_set": ["cn-ip"], "action": "route", "outbound": "direct" }
    ],
    "final": "proxy"
  }
}
```

### 11.5 规则集设计

不使用已废弃的 `geoip/geosite`，统一使用 `rule_set`。

规则集来源分两类:

- 本地内置规则集
- 远程更新规则集

第一阶段建议:

- `cn-domain.srs`
- `cn-ip.srs`

后续可扩展:

- `ads.srs`
- `private.srs`
- `custom-direct.srs`
- `custom-proxy.srs`

### 11.6 DNS 设计

DNS 是智能分流成立的关键部分。

基础方案:

- `dns-direct`: 本地或国内 DNS
- `dns-proxy`: 走代理的 DoH/DoT

规则:

1. `clash_mode = direct` -> `dns-direct`
2. `clash_mode = global` -> `dns-proxy`
3. `cn-domain` -> `dns-direct`
4. 其他域名 -> `dns-proxy`

建议启用:

- `reverse_mapping = true`
- `experimental.cache_file.store_dns = true`

目标:

- 智能分流时尽可能减少 DNS 泄漏
- TUN 模式下让域名与后续连接的关联更稳定

### 11.7 实验性功能

默认开启:

- `experimental.clash_api`
- `experimental.cache_file`

用途:

- 热切换 `mode`
- 热切换 `selector`
- 持久化上次选择的节点
- 持久化 DNS 缓存与其他状态

## 12. 模式切换策略【部分完成】

### 12.1 无重启切换

以下动作尽量不重启 `sing-box`:

- 路由模式切换 `Rule/Global/Direct`
- 手动选节点
- 自动/手动选节点模式切换

实现方式:

- 通过 Clash API 修改 `mode`
- 通过 Clash API 控制 `selector`

### 12.2 允许重启切换

以下动作允许重建配置并平滑重启内核:

- `Proxy` 与 `TUN` 接管模式切换
- 监听端口变更
- TUN 关键参数变更
- 节点库结构发生大范围变化

### 12.3 平滑重启原则

- 先生成新配置并校验
- 再启动新内核或重启旧内核
- 成功后刷新 UI 状态
- 失败时回滚到旧状态并记录日志

## 13. 进程与生命周期设计【部分完成】

### 13.1 启动

1. 初始化日志
2. 检查是否单实例
3. 加载本地设置
4. 初始化托盘
5. 如配置为开机自动连接则启动内核

### 13.2 连接

1. 构建配置对象
2. 写出 `active.json`
3. 调用 `sing-box check`
4. 启动 `sing-box.exe`
5. 检查 Clash API 与监听端口可用性
6. 更新托盘图标与提示文本

### 13.3 断开

1. 通知 UI 进入 stopping
2. 停止 `sing-box`
3. 清理系统代理
4. 更新托盘状态

### 13.4 异常退出恢复

- 捕获 `sing-box` 进程退出事件
- 若为用户主动停止，不自动重启
- 若为异常退出，显示通知并按策略重试

## 14. Windows 托盘设计【部分完成】

### 14.1 基本行为

- 应用启动后显示托盘图标
- 双击托盘图标打开主窗口
- 关闭窗口默认隐藏到托盘
- 真正退出只能通过托盘菜单触发

### 14.2 图标状态

建议使用不同图标或叠加状态:

- 灰色: 未连接
- 蓝色: 代理模式运行中
- 绿色: TUN 模式运行中
- 黄色: 切换中
- 红色: 错误

### 14.3 托盘菜单

- 连接
- 断开
- 接管方式
- 路由模式
- 节点
- 自动选择
- 打开主窗口
- 查看日志目录
- 重启内核
- 开机启动
- 退出

## 15. 权限与安全设计【部分完成】

### 15.1 原则

- 主托盘程序尽量普通权限运行
- 只有 TUN 相关操作进入提权通道
- 减少管理员权限暴露时间和范围

### 15.2 提权方式

当前仓库已落地按需提权 helper 方案，service 仍处于设计阶段。

两种候选方案:

#### 方案 A: 单独 helper

- 切换到 TUN 时按需拉起提权 helper
- 完成必要操作后退出或保持短生命周期

优点:

- 实现简单
- 适合第一阶段

缺点:

- 模式切换时可能出现 UAC 打断

#### 方案 B: 常驻 Windows service

- 安装时注册服务
- 主程序通过 IPC 发起高权限操作

优点:

- TUN 切换体验更稳定
- 权限边界更清晰

缺点:

- 部署更复杂

建议路线:

- 当前版本采用 helper
- 后续如需减少 UAC 干扰，再升级为 service

### 15.3 密钥与敏感数据

- 节点密码存储在本地配置中
- 可选使用 Windows DPAPI 加密敏感字段
- Clash API 只监听 `127.0.0.1`
- 总是设置随机 `secret`

## 16. 持久化设计【部分完成】

### 16.1 文件

- `settings.json`: 用户设置
- `nodes.json`: 节点库
- `subscriptions.json`: 订阅源
- `app-state.json`: 会话恢复与运行状态
- `active.json`: 生成的活动配置
- `cache.db`: `sing-box` cache file

### 16.2 恢复项

需恢复的状态:

- 上次选择的节点
- 上次 `RouteMode`
- 上次 `CaptureMode`
- 上次 `NodeMode`
- 是否自动连接
- 上次退出原因与恢复失败信息

### 16.3 CacheFile

使用 `sing-box experimental.cache_file` 持久化:

- selector 选择
- clash mode
- DNS cache

## 17. 日志与诊断设计【部分完成】

### 17.1 应用日志

记录:

- 启动与退出
- 配置生成摘要
- 节点切换
- 模式切换
- 提权请求
- 异常栈信息

当前已实现 `app.log` 与 `sing-box.log` 分离记录。

### 17.2 内核日志

`sing-box` 标准输出与标准错误重定向至 `sing-box.log`。

### 17.3 用户可见诊断

主窗口提供:

- 当前状态
- 当前实际出口节点
- 本地监听端口
- 最近错误
- 打开日志目录按钮

### 17.4 自检

至少提供:

- `sing-box.exe` 文件存在
- `libcronet.dll` 文件存在
- 配置校验通过
- Clash API 可访问
- 本地端口监听正常

当前额外已覆盖:

- 开机自启注册状态
- 规则集文件存在性
- 提权 helper 文件存在性
- 会话恢复状态提示

## 18. 打包与部署设计【未完成】

### 18.1 打包内容

- `EasyNaiveTray.exe`
- `EasyNaive.Elevation.exe`
- `sing-box.exe`
- `libcronet.dll`
- 默认图标与资源

### 18.2 安装器

建议使用 `WiX`。

安装器职责:

- 拷贝程序文件
- 创建卸载入口
- 可选注册自启动
- 可选安装 service

### 18.3 升级

升级时遵循:

- 不覆盖 `%LocalAppData%\EasyNaive` 下的用户数据
- 如果规则集或配置结构变更，执行迁移逻辑

## 19. 测试策略【未完成】

### 19.1 单元测试

覆盖:

- 节点模型验证
- 配置生成
- 路由模式映射
- 文件存储读写
- 状态机转换

### 19.2 集成测试

覆盖:

- 启动 `sing-box` 成功
- Clash API 可访问
- `RouteMode` 热切换
- `manual/auto` 节点切换
- `proxy/tun` 配置生成

### 19.3 手工验证

重点场景:

- 代理模式智能分流
- 代理模式全局代理
- TUN 模式智能分流
- TUN 模式全局代理
- 节点切换不中断或可恢复
- 托盘常驻和开机自启

## 20. 分阶段实施计划【已更新】

### 里程碑 M1: 基础骨架【已完成】

- 建立解决方案与项目结构
- 完成数据模型
- 完成本地配置存储
- 完成 `ConfigBuilder` 基础版

### 里程碑 M2: 代理模式 MVP【已完成】

- `mixed` 入站
- 多节点管理
- 手动节点切换
- `Rule/Global/Direct`
- 托盘常驻

### 里程碑 M3: 自动选节点【部分完成】

- `urltest`
- `manual/auto` 统一出口
- 延迟显示与最近节点菜单

### 里程碑 M4: TUN 模式【部分完成】

- 提权 helper
- TUN 启停
- TUN 下分流与 DNS 验证
- 当前已完成 `naive` 下兼容型 TUN，完整原生 UDP TUN 暂未覆盖

### 里程碑 M5: 稳定性与安装【部分完成】

- 日志与自检
- 崩溃恢复
- 安装器
- 自启动与卸载
- 当前已完成单实例、开机自启、`sing-box check`、应用日志、自检、启动恢复
- 当前未完成安装器、升级迁移、卸载与测试工程

## 21. 风险与对策【已确认】

### 21.1 Windows TUN 兼容性

风险:

- 虚拟网卡、VirtualBox、杀毒软件或系统策略影响

对策:

- 提供 `strict_route` 开关
- 提供一键退回代理模式
- 保留日志和错误提示

### 21.2 DNS 失真或泄漏

风险:

- 智能分流下 DNS 与实际连接出口不一致

对策:

- 独立设计 DNS 规则
- 启用 `reverse_mapping`
- TUN 模式重点做 DNS 验证

### 21.3 节点切换状态不同步

风险:

- UI 显示节点与实际出口节点不一致

对策:

- 使用 Clash API 查询当前 selector 状态
- 将 UI 状态视为内核状态的镜像，而不是单纯本地缓存

### 21.4 上游版本变动

风险:

- `sing-box` 升级导致配置字段变化

对策:

- 将 `ConfigBuilder` 模块化
- 通过 `patches/` 记录自定义修改
- 在升级前执行配置兼容性测试

## 22. 最终决策【已确认】

本项目采用如下最终方案:

- 客户端语言: `C# + .NET 8`
- UI: `WinForms`
- 代理内核: 外部 `sing-box.exe`
- 节点协议: `naive outbound`
- 多节点切换: `selector + urltest + Clash API`
- 路由模式: `Rule / Global / Direct`
- 接管模式: `Proxy / TUN`
- 托盘形态: 默认常驻右下角
- 状态持久化: `JSON + sing-box cache_file`
- 提权策略: 先 helper，后续视稳定性升级为 service

该方案优先保证:

- 能尽快交付可用版本
- 不重复实现代理内核
- 让 UI、配置、权限、数据面边界清晰
- 为后续订阅、规则扩展和安装部署留下空间

## 23. 后续待办清单【已更新】

本节将当前未完成项整理为可执行待办，优先级以可交付性和工程闭环为准。

### 23.1 P0: 交付闭环

- [ ] 安装打包：补齐 `packaging/` 和安装器方案，完成程序文件、`sing-box`、`libcronet.dll`、图标资源的统一打包
- [ ] 卸载与升级迁移：明确 `%LocalAppData%\\EasyNaive` 保留策略、配置迁移策略和旧版本兼容路径
- [ ] 规则集投放：补齐 `cn-domain.srs`、`cn-ip.srs` 的内置投放或下载更新逻辑，避免智能分流退化
- [ ] TUN 产品说明：在 UI 和文档中明确 `naive` 下的 TUN 为兼容型 TUN，而非完整原生 UDP VPN

### 23.2 P1: 工程化完善

- [ ] 测试工程：落地 `tests/`，至少覆盖配置生成、节点验证、文件存储、模式切换和恢复逻辑
- [ ] 平台层拆分：把当前 `EasyNaive.App` 中的平台相关逻辑下沉到 `EasyNaive.Platform.Windows`
- [ ] 自动模式状态闭环：补齐 `auto` 模式下真实出口节点的状态查询与显示
- [ ] 手工验证清单：把代理模式、TUN 模式、订阅刷新、启动恢复、自检结果整理成可重复执行的 smoke test

### 23.3 P2: 安全与演进

- [ ] 敏感数据保护：为节点密码等字段增加 DPAPI 或等效本地加密方案
- [ ] 权限收敛：评估从按需 helper 过渡到常驻 service 的收益与成本
- [ ] 完整 TUN 能力规划：如需原生 UDP TUN，增加支持原生 UDP 的节点协议，而不是继续扩展 `naive`
- [ ] 上游升级策略：补齐 `sing-box` 版本升级兼容测试和本地补丁维护流程

### 23.4 建议执行顺序

1. 先完成安装打包、卸载与升级迁移，形成可交付版本。
2. 再补规则集投放和自动模式状态闭环，减少功能“名义支持但默认退化”的情况。
3. 然后落测试工程和平台层拆分，提升后续维护成本结构。
4. 最后再处理 DPAPI、service 化和完整 TUN 协议扩展。
