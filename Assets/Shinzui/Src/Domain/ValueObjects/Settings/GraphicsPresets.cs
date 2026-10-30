namespace Shinzui.Domain.Settings
{
    /// <summary>描画品質の段階</summary>
    public enum GraphicsPreset { Low = 0, Medium = 1, High = 2, Ultra = 3, Custom = 4 }

    /// <summary>描画品質プリセットの適用と判定</summary>
    public static class GraphicsPresets
    {
        /// <summary>画面設定と演出設定を保ったまま描画品質を適用する</summary>
        /// <param name="target">適用先</param>
        /// <param name="preset">描画品質プリセット</param>
        public static void Apply(GraphicsSettings target, GraphicsPreset preset)
        {
            // カスタムと未定義の値は変更しない
            if (preset < GraphicsPreset.Low || preset > GraphicsPreset.Ultra) return;

            // 品質段階に対応する描画項目を転記する
            switch (preset)
            {
                case GraphicsPreset.Low:
                    target.AntiAliasingType = 1;
                    target.TextureQuality = 0;
                    target.TextureFilteringQuality = 0;
                    target.MeshQuality = 0;
                    target.ShadowQuality = 1;
                    target.EnableContactShadow = false;
                    target.GiAndReflectionQuality = 1;
                    target.VolumeLightQuality = 1;
                    target.EnableAo = false;
                    target.EnableSsr = false;
                    target.EnableSss = false;
                    target.EnableRayTracing = false;
                    target.EnableBloom = true;
                    target.EnableLensFlare = false;
                    break;
                case GraphicsPreset.Medium:
                    target.AntiAliasingType = 3;
                    target.TextureQuality = 1;
                    target.TextureFilteringQuality = 1;
                    target.MeshQuality = 1;
                    target.ShadowQuality = 2;
                    target.EnableContactShadow = false;
                    target.GiAndReflectionQuality = 2;
                    target.VolumeLightQuality = 1;
                    target.EnableAo = true;
                    target.EnableSsr = false;
                    target.EnableSss = true;
                    target.EnableRayTracing = false;
                    target.EnableBloom = true;
                    target.EnableLensFlare = true;
                    break;
                case GraphicsPreset.High:
                    target.AntiAliasingType = 3;
                    target.TextureQuality = 2;
                    target.TextureFilteringQuality = 2;
                    target.MeshQuality = 2;
                    target.ShadowQuality = 3;
                    target.EnableContactShadow = true;
                    target.GiAndReflectionQuality = 2;
                    target.VolumeLightQuality = 2;
                    target.EnableAo = true;
                    target.EnableSsr = true;
                    target.EnableSss = true;
                    target.EnableRayTracing = false;
                    target.EnableBloom = true;
                    target.EnableLensFlare = true;
                    break;
                case GraphicsPreset.Ultra:
                    target.AntiAliasingType = 3;
                    target.TextureQuality = 3;
                    target.TextureFilteringQuality = 3;
                    target.MeshQuality = 2;
                    target.ShadowQuality = 3;
                    target.EnableContactShadow = true;
                    target.GiAndReflectionQuality = 3;
                    target.VolumeLightQuality = 3;
                    target.EnableAo = true;
                    target.EnableSsr = true;
                    target.EnableSss = true;
                    target.EnableRayTracing = true;
                    target.EnableBloom = true;
                    target.EnableLensFlare = true;
                    break;
            }

            // 適用したプリセットを記録する
            target.Preset = preset;
        }

        /// <summary>描画項目が一致するプリセットを判定する</summary>
        /// <param name="settings">判定対象</param>
        /// <returns>一致するプリセットまたはカスタム</returns>
        public static GraphicsPreset Detect(GraphicsSettings settings)
        {
            // プリセット対象の項目だけを比較する
            var candidate = new GraphicsSettings();
            for (var preset = GraphicsPreset.Low; preset <= GraphicsPreset.Ultra; preset++)
            {
                Apply(candidate, preset);
                if (settings.AntiAliasingType == candidate.AntiAliasingType
                    && settings.TextureQuality == candidate.TextureQuality
                    && settings.TextureFilteringQuality == candidate.TextureFilteringQuality
                    && settings.MeshQuality == candidate.MeshQuality
                    && settings.ShadowQuality == candidate.ShadowQuality
                    && settings.EnableContactShadow == candidate.EnableContactShadow
                    && settings.GiAndReflectionQuality == candidate.GiAndReflectionQuality
                    && settings.VolumeLightQuality == candidate.VolumeLightQuality
                    && settings.EnableAo == candidate.EnableAo
                    && settings.EnableSsr == candidate.EnableSsr
                    && settings.EnableSss == candidate.EnableSss
                    && settings.EnableRayTracing == candidate.EnableRayTracing
                    && settings.EnableBloom == candidate.EnableBloom
                    && settings.EnableLensFlare == candidate.EnableLensFlare) return preset;
            }

            // 一致しない組み合わせはカスタムとする
            return GraphicsPreset.Custom;
        }
    }
}
