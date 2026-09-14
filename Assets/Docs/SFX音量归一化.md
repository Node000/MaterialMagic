# SFX 音量归一化

## 目标

所有已接入的 SFX 听感等响，同时保证峰值不削顶。

- 响度目标：K 加权响度 **-20**（BS.1770 系数，未做门限门控——短音效做门限无意义）。
- 峰值上限：**-1 dBFS**。
- 归一化以「每个素材一个增益」的方式实现，**不改动音频源文件**，增益写在 `Assets/Scenes/StartScene.unity` 的 `AudioManager` 组件上。

## 实现

`Assets/Scripts/AudioManager.cs`：

- `GameSfxClipEntry` 新增 `volumeDb`（Inspector 可调，0 = 保持素材原始电平）。
- 播放时应用增益：普通音走 `AudioSource.PlayOneShot(clip, gain)`，定音高音（`HitPitch`）走 `source.volume = SfxVolume * gain`。
- 全局 `SfxVolume`（设置面板音量，默认 0.8）在增益之后再乘一次，对所有音效等比例生效。

## 当前数值（2026-09 实测）

测量方式：在 Unity 内用 `AudioClip.GetData` 逐样本读取导入后的素材，计算峰值 dBFS 与 K 加权响度。

| SFX | 素材 | 归一化前峰值 | 归一化前响度 | volumeDb | 归一化后峰值 | 归一化后响度 |
|---|---|---|---|---|---|---|
| Blocked | `Assets/Audio/SFX/blocked.mp3` | -6.11 | -14.84 | -5.2 | -11.31 | -20.04 |
| Damaged | `Assets/Audio/SFX/damaged.wav` | -3.29 | -12.52 | -7.5 | -10.79 | -20.02 |
| GetCoin | `Assets/Audio/SFX/get_coin.wav` | -3.13 | -22.55 | +2.1 | -1.03 | -20.45 |
| NormalInteract | `Assets/Audio/SFX/normal_interact.wav` | -9.49 | -22.63 | +2.6 | -6.89 | -20.03 |
| HitPitch | `Assets/Audio/SFX/hit_pitch.wav` | 0.00 | -10.21 | -9.8 | -9.80 | -20.01 |
| Buy | `Assets/Audio/SFX/buy.wav` | -2.88 | -18.73 | -1.3 | -4.18 | -20.03 |
| NotEnoughMoney | `Assets/Audio/SFX/not_enough_money.mp3` | -10.06 | -23.79 | +3.8 | -6.26 | -19.99 |

归一化前响度跨度 13.6 dB，归一化后 0.46 dB（`GetCoin` 是唯一受峰值上限限制的素材，比目标低 0.45 dB）。

### 注意事项

- `hit_pitch` 原始素材峰值就是 0.00 dBFS（已削顶），且播放时 pitch 会升到 1.45（+3.2 dB）。增益 -9.8 dB 已经覆盖这部分余量；如果后续替换该素材，需要重新测。
- 正增益（大于 0 dB）只有在走 `PlayOneShot` 的通路上才成立。实测：`AudioSource.PlayOneShot(clip, volumeScale)` 的音量系数是线性的、不封顶（系数 2.0 → 输出 2.000×，3.0 → 3.000×，0.5 → 0.500×）；而 `AudioSource.volume` 被封顶到 1.0（写入 2.0 读回 1.000）。所以定音高通路（`PlaySfx(id, pitch)`）只能承担衰减；目前只有 `HitPitch` 走这条路（增益 -9.8 dB，`SfxVolume` 拉满也只有 0.324），是安全的。以后若给定音高音效配正增益，需要改走 one-shot 通路。
- 主输出Headroom（项目级，不是本次归一化引入的）：音乐素材本身峰值就是 0.00 dBFS，`musicVolumeMultiplier` 0.5 后约 -6 dBFS；SFX 里峰值最高的是 `get_coin`，按默认 `SfxVolume` 0.8 计算约 -5.0 dBFS。两者峰值重合时总和略超 0 dBFS（约 +0.5 dB）。本次归一化把 SFX 最大峰值从原来的 -1.9 dBFS 降到 -5.0 dBFS，已经比改动前宽松，但若要彻底消除，需要给音乐或主输出加统一衰减（例如挂 AudioMixer 主组，`audioMixer` 字段目前是空的）。
- `get_coin`、`buy` 属于高 crest 素材（瞬态尖、尾巴轻，crest 16~19 dB），做响度对齐后主观上仍可能偏弱，必要时可单独再上抬 1~2 dB，或对素材做 3~5 dB 的短瞬态限制。
- Unity 导入面板上的 `Normalize` 勾选不是响度归一化：实测导入相对原始 wav 只有 -0.13~+2.28 dB 的零散变化，无法代替本次对齐。
- 背景音乐（`Assets/Audio/Music/`）未改动，四首之间跨度 2.2 dB，视为可接受；`musicVolumeMultiplier` 默认 0.5 会再整体压 6 dB。

## 尚未接入的素材

| 素材 | 状态 |
|---|---|
| `Assets/Audio/SFX/Buff.wav` | 未接入：`GameSfxId` 无对应项，Buff 弹窗链路（`BuffPopupEffectController` / `BuffPopupEffectSettings`）无音频字段 |
| `Assets/Audio/SFX/Debuff.wav` | 未接入，同上 |
| `Assets/Audio/SFX/button.mp3` | 孤儿资源，按钮点击目前走 `normal_interact.wav` |

若接入这三项，按同一目标（响度 -20 / 峰值 -1）测量后填 `volumeDb`：`Buff.wav` 约 -3.1，`Debuff.wav` 约 +1.9。

## 后续素材规范

新 SFX 交付时建议直接按目标做：峰值 ≤ -1 dBTP，K 加权响度 -20 ±1 dB，44.1 kHz 单声道，短促音效果限 150 ms 以内。若素材本身 crest > 16 dB，请先做轻度瞬态控制再交付，否则靠增益无法同时满足响度对齐与不削顶。
