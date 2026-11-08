# WSRLetterBoxFix

WitchSpring R（魔女之泉 R）超宽屏黑边修复插件 —— 隐藏游戏自己画的 16:9 黑边。

Ultrawide letterbox fix for **WitchSpring R**: hides the black bars the game draws on
screens wider than 16:9.

---

## 问题：黑边到底是怎么来的

游戏在 21:9 / 32:9 之类的带鱼屏上，左右两侧会出现纯黑边。实测（2150×900 窗口）画面只有
1600×900，左右各 275px 纯黑。

原因是两层叠加，**只改分辨率或只改相机都没用**：

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

每帧遍历 Canvas 层级，找到名字以 `LetterBox` 开头的对象并 `SetActive(false)`。

相机那部分交给现成的相机修复插件处理，两者配合就能在超宽屏上铺满
（本插件只负责 UI 黑边这一半，因此可以和任何把 `camera.rect` 拉回 `(0,0,1,1)` 的插件共存）。

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

日志里看到下面这行就说明生效了：

```
[LetterBox] 已隐藏 4 个黑边面板: LetterBox_Right LetterBox_Left LetterBox_Up LetterBox_Down
```

## 构建

需要 .NET SDK（用 `net6.0` 目标框架）和游戏的 BepInEx 环境作为引用：

```powershell
dotnet build src/WSRLetterBoxFix.csproj -c Release -p:GameDir="D:\steam\steamapps\common\WitchSpring R"
```

`GameDir` 指向游戏根目录，工程会从这里引用：

- `BepInEx\core\BepInEx.Core.dll`、`BepInEx.Unity.IL2CPP.dll`、`Il2CppInterop.Runtime.dll` 等
- `BepInEx\interop\Il2Cppmscorlib.dll`、`UnityEngine.dll`、`UnityEngine.CoreModule.dll`、`UnityEngine.UIModule.dll`

产物会直接输出到 `BepInEx\plugins\WSRLetterBoxFix\`。

## 配置

首次运行会在 `BepInEx\config\witchspringr.letterboxfix.cfg` 生成：

```ini
[General]

## 隐藏游戏画在 21:9 屏幕两侧的 LetterBox 黑边面板。
# Setting type: Boolean
# Default value: true
Enabled = true
```

改成 `false` 并重启游戏即可关闭。

## 实测结果

窗口 2150×900（宽高比 2.3889，正好等于 3440:1440）：

| | 画面区域 | 黑边 |
|---|---|---|
| 修复前 | 1600×900 | 左右各 275px |
| 修复后 | 2150×900 | 无 |

标题画面与进游戏后的 3D 场景都验证过，UI 位置正常。

## 注意

- 游戏过场动画原本也用这 4 个面板做"电影黑边"，启用后过场也会变成满屏。
  想保留过场黑边就把 `Enabled` 改成 `false`。
- 本插件只操作运行时对象，**不修改任何游戏文件**，卸载删掉 dll 即可。

## 相关

- [BepInEx 6 (IL2CPP)](https://github.com/BepInEx/BepInEx)
- [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop)

## License

MIT
