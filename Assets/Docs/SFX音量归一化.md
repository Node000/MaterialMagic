# SFX 音量归一化

## 目标

所有已接入的 SFX 听感等响，同时保证峰值不削顶。

- 响度目标：K 加权响度 **-20**（BS.1770 系数，未做门限门控——短音效做门限无意义）。
- 峰值上限：**-1 dBFS**。
- 归一化以「每个素材一个增益」的方式实现，**不改动音频源文件**，增益写在 `Assets/Scenes/StartScene.unity` 的 `AudioManager` 组件上。
- SFX 导入设置统一为**单声道 + 关闭 Normalize**（详见下文“导入设置与响度口径”），让“素材电平 → 增益”这条链可复现。

## 实现

`Assets/Scripts/AudioManager.cs`：

- `GameSfxClipEntry` 新增 `volumeDb`（Inspector 可调，0 = 保持素材原始电平）。
- 播放时应用增益：普通音走 `AudioSource.PlayOneShot(clip, gain)`，定音高音（`HitPitch` / `HandHover`）走 `source.volume = SfxVolume * gain`。
- 全局 `SfxVolume`（设置面板音量，默认 0.8）在增益之后再乘一次，对所有音效等比例生效。
- `GameSfxId.HandHover` 是手牌 / 出牌区箭头 hover 音效，统一入口为 `AudioManager.PlayHandHoverSfx()`：内部做 0.07s 限流（`handHoverMinInterval`）和 ±3% pitch 抖动（`handHoverPitchJitter`，设 0 可关闭）。调用点在 `Assets/Scripts/HandCardView.cs` 的 `OnPointerEnter`，手牌与出牌区都用 `HandCardView`，所以两处 hover 都覆盖。

## 当前数值（2026-09 实测，已按统一导入设置重新测量）

测量方式：在 Unity 内用 `AudioClip.GetData` 逐样本读取导入后的素材，计算峰值 dBFS 与 K 加权响度（BS.1770 系数、未做门限）。

| SFX | 素材 | 素材峰值 | 素材响度 | volumeDb | 归一化后峰值 | 归一化后响度 |
|---|---|---|---|---|---|---|
| Blocked | `Assets/Audio/SFX/blocked.mp3` | -5.72 | -17.96 | -2.0 | -7.72 | -19.96 |
| Damaged | `Assets/Audio/SFX/damaged.wav` | -3.29 | -12.52 | -7.5 | -10.79 | -20.02 |
| GetCoin | `Assets/Audio/SFX/get_coin.wav` | -3.13 | -22.55 | +2.1 | -1.03 | -20.45 |
| NormalInteract | `Assets/Audio/SFX/normal_interact.wav` | -9.49 | -22.63 | +2.6 | -6.89 | -20.03 |
| HitPitch | `Assets/Audio/SFX/hit_pitch.wav` | 0.00 | -10.21 | -9.8 | -9.80 | -20.01 |
| Buy | `Assets/Audio/SFX/buy.wav` | -2.88 | -18.73 | -1.3 | -4.18 | -20.03 |
| NotEnoughMoney | `Assets/Audio/SFX/not_enough_money.mp3` | -10.07 | -26.80 | +6.8 | -3.27 | -20.00 |
| HandHover | `Assets/Audio/SFX/hand_hover.wav` | -1.47 | -20.11 | +0.1 | -1.37 | -20.01 |

归一化后响度全部落在 -19.96 ~ -20.45（共 0.49 dB），峰值最高 -1.03 dBFS（`GetCoin`，受峰值上限约束，比目标低 0.45 dB）。

## 导入设置与响度口径

- **Normalize 关闭**：Unity 的 `Normalize` 会改电平且行为不可预期——同一个 `hand_hover.wav`，单声道 + Normalize 开 → 峰值被抬到 0.00 dBFS（响度 -18.67）；Normalize 关 → 峰值 -1.47 dBFS（响度 -20.11），与源文件实测（峰值 -1.40）一致。关掉之后导入电平等于素材电平，重新导入 / 换机器不会再漂移。
- **统一单声道**：Unity 对单声道源做居中声像会乘 0.707（-3 dB，实测振幅 1e-5 的探针单声道输出峰值 7.071e-6），而立体声源直接通过；而 BS.1770 响度是按声道功率求和，dual-mono 立体声素材测出来会白白高 3 dB。混用单/双声道会直接造成 3 dB 对齐误差（第一版就踩到了：`blocked` / `not_enough_money` 实际比单声道素材响约 3 dB）。这批 SFX 左右声道内容一致（`max|L-R|` ≈ 1 个 16bit 量化步长），转单声道无内容损失，同时省一半解码内存。
- 因为整批素材都是单声道，-3 dB 的居中衰减对所有音效是同一个偏移量，不影响相对对齐；标称目标“-20”是素材电平口径，实际听感整体再低约 3 dB。

### 注意事项

- `hit_pitch` 原始素材峰值就是 0.00 dBFS（已削顶），且播放时 pitch 会升到 1.45（+3.2 dB）。增益 -9.8 dB 已经覆盖这部分余量；如果后续替换该素材，需要重新测。
- 正增益（大于 0 dB）只有在走 `PlayOneShot` 的通路上才成立。实测：`AudioSource.PlayOneShot(clip, volumeScale)` 的音量系数是线性的、不封顶（系数 2.0 → 输出 2.000×，3.0 → 3.000×，0.5 → 0.500×）；而 `AudioSource.volume` 被封顶到 1.0（写入 2.0 读回 1.000）。所以定音高通路（`PlaySfx(id, pitch)`）只能承担衰减；目前走这条路的 `HitPitch`（-9.8 dB）与 `HandHover`（+0.1 dB）都在安全范围（`SfxVolume` 拉满时总计 1.01，最多丢 0.1 dB）。以后若给定音高音效配较大正增益，需要改走 one-shot 通路。
- 主输出 Headroom（项目级，不是本次归一化引入的）：音乐素材本身峰值就是 0.00 dBFS，`musicVolumeMultiplier` 0.5 后约 -6 dBFS；SFX 里峰值最高的是 `get_coin`，按默认 `SfxVolume` 0.8 算约 -3.0 dBFS。两者峰值重合时总和会超出 0 dBFS（约 +1.7 dB）；改动前 `hit_pitch` 素材峰值就是 0.00 dBFS（按 0.8 算 -1.9 dBFS，超约 +2.3 dB），所以本次是变宽松而不是变更差。要彻底消除需要给音乐或主输出加统一衰减（例如挂 AudioMixer 主组，`audioMixer` 字段目前是空的）。
- `get_coin`、`buy` 属于高 crest 素材（瞬态尖、尾巴轻，crest 16~19 dB），做响度对齐后主观上仍可能偏弱，必要时可单独再上抬 1~2 dB，或对素材做 3~5 dB 的短瞬态限制。
- `hand_hover` 属于更响的一类素材（响度 -20.11，crest 18.6 dB），若实战中手牌划过偏吵，优先改 `HandHover` 的 `volumeDb`（例如调到 -3），而不是改素材；如果多个箭头快速划过仍嫌机械，调 `handHoverPitchJitter`（0.03 → 0.05）或拉长 `handHoverMinInterval`。
- 背景音乐（`Assets/Audio/Music/`）未改动，四首之间跨度 2.2 dB，视为可接受；`musicVolumeMultiplier` 默认 0.5 会再整体压 6 dB。

## 尚未接入的素材

| 素材 | 状态 |
|---|---|
| `Assets/Audio/SFX/Buff.wav` | 未接入：`GameSfxId` 无对应项，Buff 弹窗链路（`BuffPopupEffectController` / `BuffPopupEffectSettings`）无音频字段 |
| `Assets/Audio/SFX/Debuff.wav` | 未接入，同上 |
| `Assets/Audio/SFX/button.mp3` | 孤儿资源，按钮点击目前走 `normal_interact.wav` |

若接入这三项，先按同一套导入设置处理（单声道、Normalize 关）再用同样方式测量后填 `volumeDb`；不要沿用旧数值（旧测量是在 stereo + Normalize 开的导入下做的，会偏 3 dB 左右）。

## 后续素材规范

新 SFX 交付时建议直接按目标做：峰值 ≤ -1 dBTP，K 加权响度 -20 ±1 dB，单声道。时长上限：hover / UI 短音效 150 ms 以内（`hand_hover` 只有 65 ms，很适合 hover），其余音效按表现需要。若素材本身 crest > 16 dB，请先做轻度瞬态控制再交付，否则靠增益无法同时满足响度对齐与不削顶。

交付后我会把新素材按上面流程测量、填 `volumeDb`（素材本身不用自己压响度）。
