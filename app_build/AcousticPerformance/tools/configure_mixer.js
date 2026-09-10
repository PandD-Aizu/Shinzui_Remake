// A dry mixer bus: Steam Audio already supplies the world source's reflections.
var bus = studio.project.lookup('bus:/WorldSE');
if (!bus) {
    bus = studio.project.create('MixerGroup'); bus.name = 'WorldSE';
    bus.output = studio.project.workspace.mixer.masterBus;
}
var se = studio.project.lookup('vca:/SE');
var foot = studio.project.lookup('event:/FootstepPrototypeSpatial');
if (!se || !foot) throw new Error('Existing SE VCA or generated footstep event missing.');
if (bus.masters.indexOf(se) < 0) bus.relationships.masters.add(se);
foot.mixerInput.output = bus;
studio.project.save();
console.log('FootstepPrototypeSpatial -> WorldSE -> Master; WorldSE controlled by SE VCA.');
