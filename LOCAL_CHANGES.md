# 0.16.6 本地修改记录

## 0.16.6 发牌列表与五行阵旗

- 本机默认配置日志反复记录 `IllustrationLevelCardsItem.SetData/3` 安装失败；本机热更 DLL 的实际签名为四参数 `async void SetData(Level, List<CardConfig>, int, string)`。移除该异步方法与共享 `ConfigManager.GetCardConfigsByCondition` 的改写，在同步 `CardIllustrationPanel.OnSetType` / `SpecificCardIllustrationPanel.Refresh` 入口中直接填充原生行。按当前开放赛季及原生页签条件过滤，行延迟设为 0，兼容暂停中的练习场。
- 特殊列表保留 Show 请求，兼容下一帧 OnStart 再次 Refresh；澄心剑使用所请求配置，不再被原生 `rarity == 0` 过滤。
- 原生 `Card_7000111` 第一处 GetNextParam 读取灵阵，偶数格第二处读取灵印；旧本地随机规则表缺少 7000111，始终返回 -1。新增从手牌挑选的规则，灵阵复制等级按 otherParams[0] 封顶，灵印沿用手牌牌级。不消耗或降级原手牌，缺少灵阵不会取消第二次灵印读取。
- 手牌来源改为出牌角色的 publicData.lastRoundData.handCards，避免依赖显示左右方位；载入完整卡牌配置，兼容复盘旧赛季和特殊版本。
- 修复包保留 0.16.5 复盘仙命快照及其余既有功能。没有替换残局求解原生库。

验证记录见 `build/arena-0.16.6-validation.json`。本轮未启动游戏，实机显示、灵阵复制等级和偶数格效果仍需验证。

## 0.16.5 复盘仙命快照

双方的仙命列表与仙命计数改为统一来自所选轮次的 lastRoundData；导入后的 publicData.talents 同步到该轮快照，避免把结算后的仙命混入本地复盘。复盘入口与逐牌诊断继续保留。

gma6hs2pc2 第 14 轮的双方仙命已传入求解器；原生 0.2.6 漏算先天满元（125）的开战升级。把对手锟铻金环由 38 改为 10038 后，使用原版发布库复算精确得到我方 -2、对手 22，与实战吻合。sim 的开发源码修复单独保留，练习场包不替换原生求解库。

## 0.16.4 复盘逐牌诊断

为战绩 gma6hs2pc2 第 14 轮的误算记录添加只读诊断：复盘练习开打时记录轮次、指定先手；复用已有的回合和出牌检查钩子，记录双方血量、上限、防御、灵气、体魄、体魄上限、身法、气势、内伤、土灵激活和逐尘步状态。退出时补记录末态。仅在复盘导入后的本地练习里记录，最多 256 次出牌检查和 128 次回合检查，异常会停用记录。本版本没有修改求解库或战斗数值，不声称修复误算。

## 0.16.3 复盘入口修补

本机 Player-prev.log 记录了 RecordDetailPanel.Review 的前置钩子异常；加载器 ModHost.SafeHead 在格式化异常时再次空引用，原始原因没有留下，不能据此认定是某张牌或战绩数据缺失。

新增对 RecordDetailPanel.OnReviewBtnClick 的拦截：点击复盘直接使用本地导入流程，避开原生确认回调和创建服务器复盘房间流程；Review 的旧入口仍保留。导入失败时以纯文本警告及屏幕提示报告读取战绩、读取赛季、复制战绩、校验、双方导入或加载场景的阶段，避开旧加载器的异常格式化路径。离线保护、房间检查和战绩校验保持生效。

Release 编译与打包检查通过；尚未在游戏内复测，不把入口修补或新增诊断当作已经确认解决原始异常。

## 0.16.2 调整

战斗控制栏的“回大厅”改为“回到摆牌”。通过 `ArenaSession.ReturnToPlacement` 调用原生 `BattleReplayPanel.OnExitBtnClick`，让游戏停止当前执行器、等待战斗退出并切回修炼/备战阶段。练习会话保持活动，继续调整双方卡牌。设置名称同步更新，原来的 `visibility.battleLeave` 配置键保留。

此处复用了本机游戏热更 DLL 中的原生练习模式退出逻辑；已检查暂停中、执行中、已结束三种情况的代码路径，实机仍待验证。

悟剑补充独立按钮与仙命图标直达入口，编辑实际的 `BattlePlayerData.talentDatas[189].commonParams` 卡牌记录，而不是计数。支持勾选、取消、搜索、只看已悟和清空；不同牌级共用同一基础牌记录。已悟列表也修复按稀有度误排除澄心剑的问题。切换编辑方后旧面板不会误修改另一方。

澄心剑面板补充磨剑与四个已选分支的原生完整效果文本，通过 `ConfigExtension.ParseDescription` 填入实际参数，并使用当前赛季的补充说明。切换分支实时更新；不选时明确显示不追加该阶段效果。面板按可用画布缩放并自动换行。

## 来源

- [源码仓库](https://github.com/Airexplosion/yixianpai-arena)：公开 main 与 v0.16.0 源码归档都仍包含 0.14.2 的 manifest。
- [0.16.0 官方发布包](https://github.com/Airexplosion/yixianpai-arena/releases/download/v0.16.0/com.yx.arena-0.16.0.zip)：SHA256 `0b53bdd72582549d81ce4bbd10b2e885de60a3db3a1f966e8fc4a34f81cd7fef`，与下载的 mods.json 一致。
- 发布 DLL：SHA256 `9eda19f647fb60ec90266fadfe26aa9d5b533a709c4bd0efdb610dfa213c7b4e`。使用 ILSpy 9.1.0.7988 反编译，恢复新版代码后重新编译。
- 本机游戏 refs 生成日期：2026-10-03；游戏热更 DLL SHA256 `8d08b25c7feb2f5a3bf2a7f913f8fc4f8631460c6597220d832569f6137986e4`。游戏 DLL/反编译诊断文件不放进 mod 发布包。

## 0.16.0 功能保留核对

| 功能 | 恢复/保留位置 |
|---|---|
| 战績“复盘”进入练习场，导入轮次、赛季、双方数据 | Game/ReviewEntry.cs、ReviewImport.cs、ArenaSession.cs |
| 复盘期间临时数据，不覆盖普通练习配置；离开后恢复 | ArenaSession.cs、ArenaMod.SaveConfig |
| 玉瓶移入/移出离线处理与编辑 | Game/BottlePanel.cs、OfflineHooks.cs、TalentDetails.cs、Ui/SetupWindow.cs |
| 悟剑记录的查看、编辑和持久化 | ArenaSide.cs、TalentDetails.cs、Ui/SetupWindow.cs |
| 复盘的额外仙命、解锁格子、公共/私有数据 | ArenaSide.cs、ReviewImport.cs、ArenaSession.cs |
| LocalPractice 求解服务及局面序列化 | ArenaMod.OnLoad、ArenaSession.SolverReady/SolverPosition |
| 1–5 级发牌，缺失等级拒绝发牌 | CardIds.cs、ArenaConfig.cs、Game/DealHook.cs |
| 返回大厅按钮、原生退出拦截、战斗停止和清理 | Ui/ControlBar.cs、BattleBar.cs、Game/LeaveHook.cs、ArenaSession.Leave |
| 原本的离线闸、随机数、破限、步进、伤害统计 | 对应 0.16.0 反编译源码，保留原逻辑 |

`build/audit-release.ps1` 比对 DLL：原发布包 51 个顶层类型全部存在，原方法名称和参数个数均存在；仅替换 ControlBar 的两个内部布局字段。该审计确认结构保留，不证明全部行为与实机一致。构造函数因设置回调扩展而不按原签名强制比较。

## 本次修改

1. 图鉴查询仅在练习场备战时改用当前开放赛季，既不改复盘的原赛季，也不改正式对局。
2. 快捷发牌从本机完整卡牌配置生成一页当前宗门列表，不写死赛季编号或新卡编号。
3. 28 项控件显示设置持久化到 SDK 配置；设置按钮永久保留。
4. 澄心剑通过原生仙命 92（磨剑）和 93–96 家族的分支计算效果。当前游戏共 2/3/3/3 个阶段分支，加上“不选”选项。剑的基础配置 rarity 为 2，因此搜索和原生列表必须分别修复过滤。
5. 修改剑分支时删除同阶段冲突分支，保留其他仙命及计数。扩展仙命存档增加末尾两个可选字段，兼容 0.16.0 的 16 字段及更旧的 14/13/11 字段格式。
6. 构建时补齐公开 SDK 引用遗漏的两个新版签名。引用补充只供编译，发布包不包含任何 SDK、游戏 DLL 或 refs。

## 验证与剩余实测

- 184 项纯逻辑测试：赛季轮换、宗门及秘术筛选、控件布局、剑的境界版本、所有分支、计数、存档及复盘中的额外仙命保护，以及已悟卡牌的跨牌级去重、取消、清空、保存和双方独立记录。
- Release 编译无错误、无编译警告；SDK IL 检查结果保存到 build/il-check.json。IL 检查中的 AOT 泛型等警告需结合原版和实际运行环境判断。
- 游戏内待实测：单人练习入口、最新赛季图鉴、快捷发牌单页、双方切换、隐藏设置重启后保持、陆剑心分支说明/伤害/卡面、复盘导入、玉瓶、悟剑选项即时生效、残局求解与一键摆牌、战斗返回摆牌和备战返回大厅。

这是本地修改版，未上传 GitHub，也未写入游戏安装目录。
