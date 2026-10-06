UnitySSGIURP 1.1.5, upstream commit `8450297537b6` (jiaozi158/UnitySSGIURP), MIT license retained in `LICENSE.md`

Local integration targets Unity 6000.5 / URP 17.5: legacy Execute/OnCameraSetup regions are excluded on Unity 6000.4+, where those APIs were removed. RenderGraph declares depth/motion inputs before recording and tracks sample, backface and GBuffer texture access explicitly. Game cameras without post processing are excluded and a camera change invalidates temporal history. ULTRA disables SSGI inside reflection probes
