// Dedicated synthetic prototype; existing game events and previous probes are untouched.
var path='event:/FootstepPrototypeSpatial';
if(!studio.project.lookup(path)) {
    var event=studio.project.create('Event');event.name='FootstepPrototypeSpatial';event.folder=studio.project.workspace.masterEventFolder;
    var bank=studio.project.create('Bank');bank.name='GeneratedTunnelAudio';bank.folder=studio.project.workspace.masterBankFolder;
    event.relationships.banks.add(bank);
    var sound=event.addGroupTrack('Prototype foot contact').addSound(event.timeline,'SingleSound',0,.2);
    sound.audioFile=studio.project.importAudioFile('D:/Pandd/ShinShinzui/Shinzui/Assets/FootstepPrototypeSpatial.wav');
    var effect=studio.project.workspace.createPlugin('Steam Audio Spatializer');
    if(!effect)throw new Error('Steam Audio Spatializer is missing.');
    effect.owner=event.masterTrack.mixerGroup.effectChain;
    var values={DirectBinaural:1,ApplyDA:1,ApplyAA:0,ApplyDir:0,ApplyOccl:1,ApplyTrans:0,ApplyRefl:1,ApplyPath:0,DirMixLevel:1,ReflBinaural:1,ReflMixLevel:1,OutputFormat:1};
    var seen={};effect.plugin.pluginParameters.forEach(function(p){if(values.hasOwnProperty(p.name)){p.value=values[p.name];seen[p.name]=true;}});
    Object.keys(values).forEach(function(key){if(!seen[key])throw new Error('Missing DSP parameter '+key);});
    studio.project.save();console.log('Created '+path+' '+event.id);
} else { console.log('Prototype footstep event already exists.'); }
