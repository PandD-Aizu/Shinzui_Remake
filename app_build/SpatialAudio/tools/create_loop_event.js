// FMOD Studio CLI: creates only the phase-2 loop event and its dedicated bank.
var path = 'event:/SpatialAudioLoopProbe';
if (!studio.project.lookup(path)) {
    var event = studio.project.create('Event');
    event.name = 'SpatialAudioLoopProbe';
    event.folder = studio.project.workspace.masterEventFolder;
    var bank = studio.project.create('Bank');
    bank.name = 'SpatialAudioProbe';
    bank.folder = studio.project.workspace.masterBankFolder;
    event.relationships.banks.add(bank);
    var track = event.addGroupTrack('Continuous test tone');
    var sound = track.addSound(event.timeline, 'SingleSound', 0, 1);
    sound.audioFile = studio.project.importAudioFile('D:/Pandd/ShinShinzui/Shinzui/Assets/SpatialAudioLoopProbe.wav');
    event.addMarkerTrack().addRegion(0, 1, 'Continuous', studio.project.regionLoopMode.Looping);
    var effect = studio.project.workspace.createPlugin('Steam Audio Spatializer');
    if (!effect) throw new Error('Steam Audio Spatializer not loaded.');
    effect.owner = event.masterTrack.mixerGroup.effectChain;
    var values = {DirectBinaural:1, ApplyDA:1, ApplyAA:0, ApplyDir:0, ApplyOccl:1, ApplyTrans:0,
        ApplyRefl:1, ApplyPath:0, DirMixLevel:1, ReflBinaural:1, ReflMixLevel:1, OutputFormat:1};
    var seen = {};
    effect.plugin.pluginParameters.forEach(function(p) {
        if (values.hasOwnProperty(p.name)) { p.value = values[p.name]; seen[p.name] = true; }
    });
    Object.keys(values).forEach(function(key) { if (!seen[key]) throw new Error('Missing DSP parameter: ' + key); });
    var adsr = studio.project.create('ADSRModulator');
    adsr.nameOfPropertyBeingModulated = 'volume';
    adsr.initialValue = -80; adsr.peakValue = 0; adsr.sustainValue = 0;
    adsr.attackTime = 20; adsr.releaseTime = 500; adsr.holdTime = 0; adsr.decayTime = 0;
    adsr.objectBeingModulated = event.mixer.masterBus;
    studio.project.save();
    console.log('Created ' + path + ' ' + event.id);
} else { console.log('Dedicated loop event already exists.'); }
