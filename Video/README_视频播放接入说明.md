# Fran 预制视频播放接入说明

> 当前状态：Fran **尚未接入视频播放代码，也没有待播放的视频资产**。本文件只记录后续接入所需的格式、代码结构、资源生命周期和多人策略。
>
> 参考对象：本机 `StarsAbove` 的现有实现。只参考技术流程；不要把 StarsAbove 的视频资产复制进 Fran，也不要在未确认许可的情况下逐字复制其源代码。

## 1. StarsAbove 目前是怎样播放视频的

已核对的源码入口：

- `StarsAbove/StarsAbove.cs`：在客户端注册 `.ogv` 资源读取器。
- `StarsAbove/Systems/OgvReader.cs`：把模组包内的 OGV 数据转换成 FNA 的 `Microsoft.Xna.Framework.Media.Video`。
- `StarsAbove/Systems/VideoUI.cs`：用 `VideoPlayer` 播放视频，并用 `GetTexture()` 取得当前帧。
- `StarsAbove/UI/CutsceneUI/CutsceneUI.cs`：创建各个视频UI、发出开始指令、检测结束并移除UI元素。
- `StarsAbove/Systems/StarsAboveUI.cs`：建立客户端 `UserInterface`，在 `UpdateUI` 更新，并插入界面层绘制。
- `StarsAbove/Systems/Player/BossPlayer.cs`：用本地计时字段触发部分过场。

完整流程如下：

1. 模组载入时，仅在非专用服务器环境注册一个实现 `IAssetReader` 与 `ILoadable` 的 `.ogv` 读取器。
2. 读取器收到模组包内的视频流后，在主线程创建 `Video` 对象，并通过 Theorafile 打开 Ogg/Theora 数据。
3. 客户端UI初始化时，用不带扩展名的资源路径请求视频：

   ```csharp
   Asset<Video> video = ModContent.Request<Video>("Fran/Video/ExampleCutscene");
   ```

4. 开始播放时创建或复用 `VideoPlayer`，先设置 `IsLooped`，再调用 `Play(video.Value)`。
5. 每次绘制时调用 `VideoPlayer.GetTexture()` 取得当前视频帧，把它当作普通 `Texture2D` 绘制到全屏UI矩形。
6. `VideoPlayer.State` 不再是 `Playing` 后，把视频标为完成、移除对应UI，并停止和释放播放器。

StarsAbove 的 `UIVideo` 把相当一部分播放状态推进写在 `DrawSelf` 中。Fran若正式接入，建议把“开始、完成、跳过、超时”等状态放在 `UpdateUI` 或独立客户端播放器中；`DrawSelf` 只负责绘制当前帧。这样即使某个界面层暂时不绘制，控制状态也不会依赖绘制顺序。

## 2. 需要准备什么格式的视频

### 必需格式

- 文件扩展名：`.ogv`
- 容器：Ogg
- 视频编码：Theora
- 推荐像素格式：`yuv420p`
- 可选音频编码：Vorbis
- 资源位置：`Fran/Video/文件名.ogv`
- 代码请求路径：`Fran/Video/文件名`，**不要写 `.ogv` 扩展名**。

StarsAbove 的九个现有样本都包含 Theora 视频轨；其中八个包含 Vorbis 音轨，`StarfarerIntroCutscene.ogv` 只有视频轨。因此Fran既可以制作带声音的视频，也可以制作无声视频。

推荐控制在 1280×720、30 FPS 或更低；分辨率最好为偶数尺寸。这里不是解码器的绝对上限，而是基于模组体积、显存、解码开销和全屏UI观感作出的安全预算。短过场优先，避免直接塞入几百MB的长视频。

可用 FFmpeg 转换，例如：

```text
ffmpeg -i input.mp4 -c:v libtheora -q:v 7 -pix_fmt yuv420p -r 30 -c:a libvorbis -q:a 5 -ar 48000 output.ogv
```

无声版本把音频参数替换为 `-an`：

```text
ffmpeg -i input.mp4 -c:v libtheora -q:v 7 -pix_fmt yuv420p -r 30 -an output.ogv
```

转换后至少要检查：文件能够被独立播放器播放、画面比例正确、有声版本声画同步、文件名大小写与代码路径完全一致。

### 内存注意事项

StarsAbove 的读取器会先把整个压缩视频复制进 `MemoryStream`，随后再申请同等大小的非托管内存供 Theorafile 使用。也就是说，视频文件越大，载入期间的托管与非托管内存峰值越高。Fran若沿用相同架构，应把单个视频控制得足够小，并在卸载或播放结束后可靠释放 `VideoPlayer` 与读取器持有的数据。

## 3. Fran正式接入时需要增加哪些代码

建议拆成四个职责明确的文件，不要把全部逻辑塞进现有武器或玩家类：

```text
FranModMain.cs                         注册客户端OGV读取器
Video/FranOgvReader.cs                .ogv -> Asset<Video>
Content/UI/FranVideoPlayerElement.cs  VideoPlayer生命周期和逐帧绘制
Content/UI/FranVideoUISystem.cs       播放请求、界面层、跳过和结束回调
```

### 3.1 工程设置

StarsAbove 的读取器包含指针与 `UnmanagedMemoryStream` 操作。若Fran实现相同的读取方式，需要在 `Fran.csproj` 中加入：

```xml
<PropertyGroup>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

这条设置只有真正加入读取器时才需要；当前Fran没有必要提前打开。

### 3.2 注册读取器

在 `FranModMain` 中走客户端分支注册读取器，结构应与下面一致：

```csharp
public override IContentSource CreateDefaultContentSource()
{
    if (!Main.dedServ)
        AddContent(new FranOgvReader());

    return base.CreateDefaultContentSource();
}
```

`FranOgvReader` 需要完成这些责任：

- 声明只处理 `.ogv`。
- 实现 `IAssetReader.FromStream<T>`，并拒绝 `Video` 以外的目标类型。
- 等待 `MainThreadCreationContext` 后再接触图形设备或创建 `Video`。
- 用 Theorafile 回调读取、定位和关闭模组包内的数据流。
- 正确填写 `Video` 所需的宽高、YUV平面尺寸、帧率、图形设备和时长字段。
- 在关闭回调中释放 `UnmanagedMemoryStream` 及其非托管内存。
- 卸载时只移除自己注册的扩展名映射，不应误删其他模组的读取器。

注意：StarsAbove 这一读取器通过反射访问FNA `Video` 的内部字段。这不是稳定公开API，tModLoader/FNA升级后必须重新编译并实测；不要把“能编译”当成“视频一定能解码”。

### 3.3 客户端播放器

播放器至少需要这些状态：

```csharp
Asset<Video> video;
VideoPlayer player;
bool active;
bool started;
bool finished;
```

推荐生命周期：

1. UI初始化或首次需要时请求一次 `Asset<Video>`，不要每帧 `Request`。
2. `Start` 时确认当前不是专用服务器，创建 `VideoPlayer`，设置循环、音量等选项后调用 `Play`。
3. `Update` 中检测播放完成、用户跳过、世界退出或超时。
4. `DrawSelf` 中仅在播放器有效且有当前帧时调用 `GetTexture()` 并绘制。
5. `Stop`、`OnDeactivate`、世界卸载和模组卸载都必须执行 `Stop()`、`Dispose()` 并清空引用。
6. 解码失败、资源缺失或设备不支持时，立即关闭视频UI并进入静态图片/黑场淡出的降级路线，不能卡住游戏流程。

全屏播放一般使用UI缩放，并把当前帧绘制到屏幕矩形。若不希望画面变形，应按视频和屏幕宽高比计算等比缩放，再用黑边填补剩余区域；StarsAbove 的 `ScaleToFit` 是直接拉伸填满。

### 3.4 UI系统

视频UI必须是客户端系统：

```csharp
[Autoload(Side = ModSide.Client)]
public sealed class FranVideoUISystem : ModSystem
```

它应当：

- 在 `Load` 中仅为客户端创建 `UIState` 与 `UserInterface`。
- 在 `UpdateUI` 中推进播放器与淡入淡出状态。
- 在 `ModifyInterfaceLayers` 中加入专用视频层。
- 明确决定视频位于游戏UI、鼠标和字幕的上方还是下方。
- 播放期间按需求屏蔽鼠标输入，但必须保留明确的“跳过”按键。
- `OnWorldUnload` 和 `Unload` 无条件停止视频并释放资源。

## 4. 多人游戏：同步什么，不同步什么

### 先明确 StarsAbove 的现状

StarsAbove 已经正确地把读取器和UI排除在专用服务器之外，但其过场进度主要是 `Main.LocalPlayer` 上递减的本地字段。现有源码没有为视频帧、`VideoPlayer` 状态或这些过场计时提供完整的网络同步。因此它适合参考“如何解码和绘制”，**不适合直接当作Fran多人同步范本**。

视频文件和视频帧都不应该通过模组数据包发送。每个客户端安装的 `.tmod` 已经包含同一份视频；网络只发送很小的演出事件。

### 推荐为每段视频声明一种策略

#### A. `LocalCosmetic`：本地纯演出

适用于打开菜单、获得能力、个人剧情提示等只影响一个玩家的内容。

- 只在目标客户端开始播放。
- 不同步播放帧，也不要求其他玩家同时观看。
- 结束或跳过只关闭本地UI。
- 服务端战斗和世界状态完全不等待视频。

这是风险最低、最适合Fran首次接入的方式。

#### B. `SynchronizedPresentation`：服务器广播演出

适用于Boss转阶段等希望所有在场玩家都看到的演出。

- 服务器拥有唯一的演出事件和游戏逻辑状态。
- 服务器向有关客户端发送：`VideoId`、唯一 `SequenceId`、服务端开始Tick，以及是否允许跳过。
- 客户端收到后按 `SequenceId` 去重，在本地播放同一资源。
- 不发送视频字节，不逐帧同步，也不发送 `VideoPlayer.GetTexture()` 结果。
- 真正的Boss无敌、阶段切换、伤害窗口和恢复战斗时刻全部由服务器固定计时控制。
- 任何客户端加载失败、延迟、切出窗口或提前跳过，都不能改变服务器战斗时间线。

FNA这条简单播放路线没有可靠的任意时间点跳转接口。迟加入玩家应由协议明确选择“直接跳过并显示静态过渡画面”或“在很短的宽限期内从头播放”；不要声称能够通过一个开始Tick精确追帧。

#### C. `SinglePlayerOnly`：多人完全排除

如果某段演出暂时没有设计多人行为，可以明确写成仅单人：

```csharp
if (Main.netMode != NetmodeID.SinglePlayer)
    return;
```

但排除视频后，多人仍必须正常推进对应技能、Boss阶段和任务状态。视频只能是附加表现，不能成为继续游戏的必要条件。

### 专用服务器硬性规则

- `Main.dedServ` 时不得注册OGV读取器、请求 `Video`、创建 `VideoPlayer`、访问 `Main.graphics` 或创建视频UI。
- 服务端只处理“是否触发演出”和真正的玩法状态。
- 数据包处理函数必须先检查运行端；服务器收到客户端“播放完成”消息时也不能盲目信任它来推进关键进度。
- 若设计需要等待玩家确认，必须有服务器超时和断线处理，不能无限等待某个客户端。

## 5. 建议的数据包结构

只在选择 `SynchronizedPresentation` 时增加数据包。建议至少包含：

```text
PlayVideo
  byte  VideoId
  uint  SequenceId
  uint  ServerStartTick
  byte  Flags            // 可跳过、是否遮挡输入等

StopVideo
  uint  SequenceId
  byte  Reason           // 正常结束、服务器取消、世界退出等
```

客户端本地跳过通常不需要通知服务器；它只停止自己的画面。若确实要统计玩家确认，可以额外发送确认包，但服务器必须验证玩家身份、事件编号，并保留强制超时。

## 6. 正式接入前的验证清单

- 单人：能够播放、结束、跳过、重复触发，不黑屏、不残留输入锁。
- 分辨率：窗口缩放、全屏、不同UI比例下画面没有错误拉伸。
- 音频：有声与无声OGV都能播放；切出窗口和主音量变化不会留下异常声音。
- 资源失败：故意使用错误路径或损坏文件时，玩法仍继续并显示降级画面。
- 热重载：重载模组后没有残留 `VideoPlayer`、非托管内存或图形资源异常。
- 主机与远程客户端：只同步演出事件，战斗状态由服务器一致推进。
- 专用服务器：无图形设备环境能够正常加载模组并运行事件逻辑。
- 迟加入、断线、死亡、传送和退出世界：视频UI都会及时清理。
- 性能：记录视频文件大小、载入峰值、播放时帧率和垃圾回收抖动。

## 7. 当前结论

Fran以后可以沿用“OGV读取器 + FNA `VideoPlayer` + 客户端UI”的总体路线，但应当重新实现一个属于Fran的、可降级的视频模块。第一段原型建议先做 `LocalCosmetic` 的短无声720p视频，证明解码、缩放、跳过、卸载和内存回收都稳定后，再考虑服务器广播型Boss过场。
