# PR #72 conflict resolution

Merged main (da7f36c) into feature/Ryota/adjustmentTunelScale#64.

- Kept the PR's removal of the legacy static generator and settings component.
- Preserved main's floor progression through the scene LifetimeScope and an explicit Application use case for regeneration.
- Kept startup idempotent. Regeneration uses the existing geometry-clearing, navigation rebuild and audio notification sequence.
- Migrated BotamochiStageTemp while preserving component file IDs and girlfriend/enemy/dialogue references.
- Resolved the LatestStageGenerateTemp scene's whitespace-only conflicts without dropping either side's changes.
- Kept View independent from Presentation and Presentation independent from Domain/Infrastructure.

Src/ and StageTunnelGenerationTests.cs are snapshots of the corresponding Assets/Shinzui files, saved here in accordance with agents.md. The Assets files are the Unity compilation inputs.

Validation: Unity 6000.5.8f1 PlayMode StageTunnelGenerationTests; results in playmode-results.xml. The synthetic warp-player test disables its audio binding so missing external FMOD banks do not obscure warp subscription assertions. This does not verify audio playback or the full girlfriend dialogue flow.

Final result: 4 tests passed, 0 failed. Unity compilation completed without C# errors. Scene component IDs/references and source snapshot equality checks passed.
