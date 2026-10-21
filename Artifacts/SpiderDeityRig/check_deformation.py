import bpy,numpy as np,json
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=r'D:/Pandd/ShinShinzui/Artifacts/SpiderDeityRig/SpiderDeity_Rigged.blend');mesh=bpy.data.objects['SpiderDeity_Mesh'];arm=bpy.data.objects['SpiderDeity_Rig'];base=np.array([v.co[:] for v in mesh.data.vertices]);edges=np.array([e.vertices[:] for e in mesh.data.edges]);length=np.linalg.norm(base[edges[:,0]]-base[edges[:,1]],axis=1);result=[]
for side in ['L','R']:
 for n in range(1,5):
  name=f'Leg_{side}{n:02d}';t=bpy.data.objects['IK_'+name];old=t.location.copy();t.location+=Vector((0,-.025,.09));c=arm.pose.bones[name+'_Foot'].constraints[0];c.influence=1;bpy.context.view_layer.update();ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());v=np.array([p.co[:] for p in ev.data.vertices]);new=np.linalg.norm(v[edges[:,0]]-v[edges[:,1]],axis=1);ratio=new/np.maximum(length,1e-6);i=np.argmax(ratio);result.append({'leg':name,'max_edge_ratio':float(ratio[i]),'edge_midpoint':base[edges[i]].mean(0).tolist(),'original_edge_length':float(length[i]),'large_stretch_edges':int(sum((ratio>2)&(new-length>.005))) });c.influence=0;t.location=old;bpy.context.view_layer.update()
assert all(r['large_stretch_edges']==0 for r in result),result
with open(r'D:/Pandd/ShinShinzui/Artifacts/SpiderDeityRig/deformation_validation.json','w') as out:json.dump(result,out,indent=2)
print(json.dumps(result,indent=2))
