// Run with FMOD Studio 2.03 CLI against Shinzui/Shinzui.fspro.
// Only creates the dedicated OrganicReverbProbe bank/event; existing events are untouched.
var eventPath = "event:/OrganicReverbProbe";
if (studio.project.lookup(eventPath)) {
    var existing = studio.project.lookup(eventPath);
    existing.masterTrack.mixerGroup.effectChain.effects.forEach(function(effect) {
        if (effect.isOfType("PluginEffect")) effect.plugin.pluginParameters.forEach(function(p) {
            if (p.name === "ApplyOccl") p.value = 1;
            if (p.name === "OutputFormat") p.value = 1;
        });
    });
    studio.project.save();
    console.log("Updated the dedicated probe DSP configuration.");
} else {
    var event = studio.project.create("Event");
    event.name = "OrganicReverbProbe";
    event.folder = studio.project.workspace.masterEventFolder;
    var bank = studio.project.create("Bank");
    bank.name = "OrganicReverbProbe";
    bank.folder = studio.project.workspace.masterBankFolder;
    event.relationships.banks.add(bank);
    var track = event.addGroupTrack("Dry impulse");
    var sound = track.addSound(event.timeline, "SingleSound", 0, 6);
    sound.audioFile = studio.project.importAudioFile("D:/Pandd/ShinShinzui/Shinzui/Assets/OrganicReverbProbe.wav");
    var effect = studio.project.workspace.createPlugin("Steam Audio Spatializer");
    if (!effect) throw new Error("Steam Audio Spatializer was not loaded.");
    effect.owner = event.masterTrack.mixerGroup.effectChain;
    var values = {DirectBinaural:1, ApplyDA:1, ApplyAA:0, ApplyDir:0,
        ApplyOccl:1, ApplyTrans:0, ApplyRefl:1, ApplyPath:0,
        DirMixLevel:1, ReflBinaural:1, ReflMixLevel:1, OutputFormat:1};
    var seen = {};
    effect.plugin.pluginParameters.forEach(function(p) {
        console.log(p.name + "=" + p.value);
        if (values.hasOwnProperty(p.name)) { p.value = values[p.name]; seen[p.name] = true; }
    });
    Object.keys(values).forEach(function(key) {
        if (!seen[key]) throw new Error("Required DSP parameter missing: " + key);
    });
    studio.project.save();
    console.log("Created " + eventPath + " " + event.id);
}
