# SFX 增益与测量口径

`Assets/Scripts/AudioManager.cs`的gameSfxClips序列化绑定音频与volumeDb；播放将dB转线性增益并乘SfxVolume。普通音PlayOneShot与变调AudioSource.volume通路不同，后者有上限，不能假设大正增益都能生效。

已有HandHover=8，由HandCardView.OnPointerEnter调用PlayHandHoverSfx，受handHoverMinInterval与handHoverPitchJitter控制。GameSfxId显式数值被序列化，新项追加不能重排。连击挥击按连续施法变pitch，Combo文本不代表有独立音效。

历史测量采用导入后AudioClip.GetData，K加权无门控短音效口径，目标约-20、峰值≤-1dBFS；那是单素材方法，不能当主输出或当前所有音频已验收。旧表和素材候选已归档，当前增益从StartScene AudioManager读，不复制历史数值回写。

替换素材先确认导入后的声道与Normalize口径一致，再测峰值/响度并填目标volumeDb；不改源音频来冒充Inspector增益。单/双声道和声像可能引入约3dB差异。音乐、连续金币与其他叠音需要实际听感/主输出余量判断，不能用单片不削顶证明叠加安全。

本轮仅重建说明，未重新测量、试听、修改导入器或场景增益。方法入口见presentation模块；旧“箭头悬停完全无音效”已失效。
