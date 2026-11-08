# WSRLetterBoxFix

WitchSpring R（魔女之泉 R）超宽屏修复插件：**相机视口 + 黑边面板，二合一**。

Ultrawide fix for **WitchSpring R** (BepInEx 6 IL2CPP) — removes both the camera
letterboxing and the black `LetterBox_*` UI panels the game draws on screens wider than 16:9.

## 下载

编译好的插件在 [Releases](https://github.com/xujinglie/WSRLetterBoxFix/releases/latest)：

- `WSRLetterBoxFix.dll` → 放进 `<游戏目录>\BepInEx\plugins\WSRLetterBoxFix\`

需要先装好 BepInEx 6（IL2CPP 版），详见下面「安装」。

---

## 问题：黑边到底是怎么来的

游戏在 21:9 / 32:9 之类的带鱼屏上，左右两侧会出现纯黑边。实测（2150×900 窗口）画面只有
1600×900，左右各 275px 纯黑。

原因是**两层叠加**，只改分辨率或只改相机都没用：

1. **相机视口被裁成 16:9**
   主相机的 `camera.rect` 被游戏设成约 `(0.128, 0, 0.744, 1)`，两侧留空。

2. **Canvas 里挂着 4 个纯黑 UI 面板**
   ```
   Canvas
     ├─ LetterBox_Left    Image rgba=(0,0,0,1) 2000x2000
     ├─ LetterBox_Right   Image rgba=(0,0,0,1) 2000x2000
     ├─ LetterBox_Up      Image rgba=(0,0,0,1) 4000x1000
     └─ LetterBox_Down    Image rgba=(0,0,0,1) 4000x1000
   ```
   黑边其实是游戏用 UI 画出来的，不是相机清屏色，也不是渲染分辨率的问题。

## 这个插件做什么

每帧做两件事：

- **相机**：把主相机的 `rect` 拉回 `(0,0,1,1)`，并让 `aspect` 跟随窗口；
  游戏改回去就再拉回来。
- **黑边**：遍历 Canvas 层级，把名字以 `LetterBox` 开头的对象 `SetActive(false)`；
  场景切换后对象重建会自动重扫。

> v2.0.0 起把原来分开的两个插件（相机修复 `WSMenuMod.Ultrawide` + 黑边隐藏 `WSRLetterBoxFix`）
> 合并成一个，装一个就够。

## 环境要求

- 游戏：WitchSpring R（Steam 版）
- **BepInEx 6.0.0-be.x（IL2CPP 版）** + UnityDoorstop
  - `doorstop_config.ini` 里 `coreclr_path = dotnet\coreclr.dll`
  - 需要 `BepInEx\interop` 下的 Il2CppInterop 程序集

## 安装

1. 游戏目录装好 BepInEx 6 IL2CPP（能正常生成 `BepInEx\LogOutput.log`）
2. 把 `WSRLetterBoxFix.dll` 放到：
   ```
   <游戏目录>\BepInEx\plugins\WSRLetterBoxFix\WSRLetterBoxFix.dll
   ```
3. 启动游戏

> **注意**：如果之前装过 `WSMenuMod.Ultrawide`（相机修复），请先删掉它，
> 否则两个插件会重复修改相机。本插件已经包含它的功能。

日志里看到下面两行就说明生效了：

```
[黑边] 已隐藏 4 个黑边面板: LetterBox_Right LetterBox_Left LetterBox_Up LetterBox_Down
[相机] 窗口 2150x900，rect (0.128,0,0.744,1) -> (0,0,1,1)，aspect 1.778 -> 2.389
```

## 配置

首次运行会在 `BepInEx\config\witchspringr.letterboxfix.cfg` 生成：

```ini
[General]
## 总开关。关闭后本插件不修改任何东西。
Enabled = true

## 隐藏游戏用 UI 画的 LetterBox 黑边面板。
HideLetterBox = true

[Camera]
## 每帧把相机视口拉回 (0,0,1,1)。
ForceCamera = true

## 让相机纵横比跟随窗口（推荐）。设为 false 则保持 16:9 投影，画面会被横向拉伸。
ForceAspect = true

[Resolution]
## 强制渲染分辨率（默认关，一般不需要）。
ForceResolution = false
ResWidth = 3440
ResHeight = 1440
```

## 构建

需要 .NET SDK（`net6.0` 目标框架）和游戏的 BepInEx 环境作为引用：

```powershell
dotnet build src/WSRLetterBoxFix.csproj -c Release -p:GameDir="D:\steam\steamapps\common\WitchSpring R"
```

`GameDir` 指向游戏根目录，工程会从这里的 `BepInEx\core` 和 `BepInEx\interop` 引用依赖，
产物直接输出到 `BepInEx\plugins\WSRLetterBoxFix\`。

也可以直接用仓库里的脚本：

```powershell
.\build.ps1 -GameDir "D:\steam\steamapps\common\WitchSpring R"
```

## 实测结果

窗口 2150×900（宽高比 2.3889，正好等于 3440:1440）：

| | 画面区域 | 黑边 |
|---|---|---|
| 修复前 | 1600×900 | 左右各 275px |
| 修复后 | 2150×900 | 无 |

标题画面与进游戏后的 3D 场景都验证过，UI 位置正常。

## 注意

- 游戏过场动画原本也用这 4 个面板做"电影黑边"，启用后过场也会变成满屏。
  想保留过场黑边就把 `HideLetterBox` 改成 `false`。
- 本插件只操作运行时对象，**不修改任何游戏文件**，卸载删掉 dll 即可。

## 更新日志

### v2.0.0

- 合并相机修复（原 `WSMenuMod.Ultrawide`）与黑边隐藏两个插件，改为单插件
- 配置拆成 `[General]` / `[Camera]` / `[Resolution]` 三段，新增总开关 `Enabled`
- 日志前缀区分 `[相机]` / `[黑边]`

### v1.0.0

- 首个版本：隐藏 4 个 `LetterBox_*` 黑边面板

## 致谢

- 相机修复部分的思路与实现来自 `WSMenuMod.Ultrawide`
- [BepInEx 6 (IL2CPP)](https://github.com/BepInEx/BepInEx)
- [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop)

## License

MIT
