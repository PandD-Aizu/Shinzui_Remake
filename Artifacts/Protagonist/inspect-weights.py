import bpy,json
bpy.ops.wm.open_mainfile(filepath='D:/Pandd/ShinShinzui/Artifacts/Protagonist/ProtagonistRig.blend')
o=bpy.data.objects['BodyMesh']
bad=[]
for v in o.data.vertices:
    weights=[(o.vertex_groups[g.group].name,round(g.weight,4)) for g in v.groups if g.weight>.01]
    if v.co.z<1.2 and any(('Arm' in n or 'Hand' in n or 'Proximal' in n) for n,w in weights):
        bad.append((v.index,list(v.co),weights))
print('BAD',json.dumps(bad))
