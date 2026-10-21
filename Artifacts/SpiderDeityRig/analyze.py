import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d.art3d import Poly3DCollection
m=np.load('Artifacts/SpiderDeityRig/mesh.npz');v=m['v'];f=m['f'];edges=np.vstack([f[:,[0,1]],f[:,[1,2]],f[:,[2,0]]]); g=coo_matrix((np.ones(len(edges)),(edges[:,0],edges[:,1])),shape=(len(v),len(v)))
n,lab=connected_components(g,directed=False); sizes=np.bincount(lab); order=np.argsort(-sizes);np.savez('Artifacts/SpiderDeityRig/components.npz',labels=lab,sizes=sizes)
for c in order[:30]:
 q=v[lab==c]; print(int(c),len(q),'center',np.round(q.mean(0),3),'bounds',np.round(q.min(0),3),np.round(q.max(0),3))
fig=plt.figure(figsize=(14,9));ax=fig.add_subplot(projection='3d');p=v[:,[0,2,1]];tris=p[f];norm=np.cross(tris[:,1]-tris[:,0],tris[:,2]-tris[:,0]);norm/=np.maximum(np.linalg.norm(norm,axis=1)[:,None],1e-8);shade=.35+.6*np.abs(norm@np.array([.3,-.5,.81]));colors=np.column_stack([shade*.85,shade*.88,shade,np.ones(len(f))]);ax.add_collection3d(Poly3DCollection(tris,facecolors=colors,linewidth=0));ax.set_xlim(-.45,.45);ax.set_ylim(-.45,.45);ax.set_zlim(0,1);ax.set_box_aspect((.9,.9,1));ax.view_init(22,-65);fig.tight_layout();fig.savefig('Artifacts/SpiderDeityRig/source_solid.png',dpi=150)
