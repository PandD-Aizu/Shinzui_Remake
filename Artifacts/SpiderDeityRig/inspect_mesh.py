import json,struct,pathlib,numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
p=pathlib.Path(r'C:/Users/koton/Downloads/spider deity 3d model.glb').read_bytes()
n=struct.unpack_from('<I',p,12)[0]; j=json.loads(p[20:20+n]); b=p[28+n:]
def acc(i):
 a=j['accessors'][i]; v=j['bufferViews'][a['bufferView']]; dt={5126:'<f4',5125:'<u4',5123:'<u2'}[a['componentType']]; k={'VEC3':3,'SCALAR':1}[a['type']]
 return np.frombuffer(b,dtype=dt,count=a['count']*k,offset=v.get('byteOffset',0)+a.get('byteOffset',0)).reshape(-1,k)
v=acc(0); f=acc(2).reshape(-1,3); np.savez('Artifacts/SpiderDeityRig/mesh.npz',v=v,f=f)
print('Bounds',v.min(0),v.max(0),'verts',len(v),'faces',len(f)); print('materials',j.get('materials'))
fig,axs=plt.subplots(1,3,figsize=(18,7))
for ax,(a,c) in zip(axs,[(0,1),(0,2),(2,1)]):
 ax.triplot(v[:,a],v[:,c],f,color='#555555',lw=.15); ax.set_aspect('equal'); ax.set_xlabel('XYZ'[a]); ax.set_ylabel('XYZ'[c]); ax.grid(alpha=.3)
fig.tight_layout();fig.savefig('Artifacts/SpiderDeityRig/source_views.png',dpi=150)
