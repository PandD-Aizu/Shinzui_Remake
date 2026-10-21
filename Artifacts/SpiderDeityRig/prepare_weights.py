import numpy as np,json
from scipy.spatial import cKDTree
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import dijkstra
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
m=np.load('Artifacts/SpiderDeityRig/mesh.npz');v=m['v'].astype(float);f=m['f'];c=np.load('Artifacts/SpiderDeityRig/components.npz');lab=c['labels'];core=np.where(lab==0)[0]
right=[[[.048,.447,.16],[.103,.535,.275],[.174,.237,.396],[.192,0,.414]],[[.077,.44,.105],[.183,.65,.18],[.287,.372,.302],[.322,0,.346]],[[.093,.449,.044],[.257,.675,.006],[.382,.394,-.111],[.374,.024,-.158]],[[.10,.472,-.055],[.200,.700,-.194],[.248,.441,-.327],[.243,.003,-.363]]]
legs=[]
for side,sign in [('L',1),('R',-1)]:
 for i,p in enumerate(right):
  a=np.array(p);a[:,0]*=sign;legs.append({'name':f'Leg_{side}{i+1:02d}','points':a.tolist()})
def segdist(p,a,b):
 t=np.clip((p-a)@(b-a)/np.sum((b-a)**2),0,1);return np.linalg.norm(p-a-t[:,None]*(b-a),axis=1),t
D=[]
for leg in legs:
 p=np.array(leg['points']);D.append(np.min([segdist(v,a,b)[0] for a,b in zip(p[:-1],p[1:])],axis=0))
D=np.array(D).T; mind=D.min(1)
edges=np.vstack([f[:,[0,1]],f[:,[1,2]],f[:,[2,0]]]);edges=np.unique(np.sort(edges,axis=1),axis=0);w=np.linalg.norm(v[edges[:,0]]-v[edges[:,1]],axis=1);g=coo_matrix((w,(edges[:,0],edges[:,1])),shape=(len(v),len(v))).tocsr();g=g+g.T
seeds=[np.where((lab==0)&(mind>.055))[0]]
for i,leg in enumerate(legs):
 p=np.array(leg['points']);away=np.linalg.norm(v-p[0],axis=1)>.085
 seeds.append(np.where((lab==0)&(D[:,i]<.027)&(D.argmin(1)==i)&away)[0])
dists=np.array([dijkstra(g,indices=s,directed=False,min_only=True) for s in seeds]);labels=dists.argmin(0)
# Keep the humanoid upper body, halo, and abdominal shell rigid.
labels[(lab==0)&(v[:,1]>.76)]=0
# Assign disconnected ornaments by their upper attachment, to avoid dragging a chain across legs.
coretree=cKDTree(v[core])
for ci in np.unique(lab):
 if ci==0:continue
 ids=np.where(lab==ci)[0];q=v[ids]; h=q[:,1].max();top=q[q[:,1]>h-.008];anchor=top.mean(0);dist,near=coretree.query(anchor,k=5);labels[ids]=int(np.bincount(labels[core[near]],minlength=9).argmax())
# Weights blend only at joints on the same leg. Other limbs never share weights.
names=['Body']+[leg['name']+'_'+s for leg in legs for s in ['Upper','Lower','Foot']]
weights=np.zeros((len(v),len(names)))
weights[labels==0,0]=1
for k,leg in enumerate(legs):
 ids=np.where(labels==k+1)[0];p=np.array(leg['points']);q=v[ids];ds=np.array([segdist(q,a,b)[0] for a,b in zip(p[:-1],p[1:])]).T;closest=ds.argmin(1);lens=np.linalg.norm(np.diff(p,axis=0),axis=1);arc=np.r_[0,np.cumsum(lens)];t=np.array([segdist(q,a,b)[1] for a,b in zip(p[:-1],p[1:])]).T;u=arc[closest]+t[np.arange(len(q)),closest]*lens[closest];local=np.zeros((len(q),4));local[np.arange(len(q)),closest+1]=1
 for joint,width in [(0,.032),(1,.024),(2,.022)]:
  delta=u-arc[joint];blend=np.abs(delta)<width;alpha=np.clip((delta[blend]+width)/(2*width),0,1);alpha=alpha*alpha*(3-2*alpha);local[blend]=0;local[blend,joint]=1-alpha;local[blend,joint+1]=alpha
 weights[ids,0]=local[:,0];weights[ids,1+3*k:4+3*k]=local[:,1:]
# Smooth transitions over the connected surface, confined to joint seams.
dominant=weights.argmax(1)
boundary_edges=edges[(dominant[edges[:,0]]!=dominant[edges[:,1]]) & (lab[edges[:,0]]==0)]
boundary=np.unique(boundary_edges)
distance=dijkstra(g,indices=boundary,directed=False,min_only=True)
smooth=(lab==0)&(distance<.05)
adj=g.copy();adj.data=1/np.maximum(adj.data,.003)
row=np.asarray(adj.sum(1)).ravel();fixed=weights.copy()
for iteration in range(25):
 average=(adj@weights)/np.maximum(row[:,None],1e-9)
 weights[smooth]=.10*fixed[smooth]+.90*average[smooth]
# Bound exported weights to four influences, consistent with Unity's default skinning.
for i in np.where(smooth)[0]:
 weights[i,np.argsort(weights[i])[:-4]]=0
 weights[i,weights[i]<.0001]=0
 weights[i]/=weights[i].sum()
# Rigid disconnected decorations inherit weights at the attachment, keeping beads/chains intact.
for ci in np.unique(lab):
 if ci==0:continue
 ids=np.where(lab==ci)[0];q=v[ids];anchor=q[q[:,1]>q[:,1].max()-.008].mean(0);_,ni=coretree.query(anchor);weights[ids]=weights[core[ni]]
np.savez('Artifacts/SpiderDeityRig/weights.npz',weights=weights,labels=labels)
json.dump({'legs':legs,'weight_names':names,'source_coordinates':'glTF Y-up; front +Z; L = model left (+X)'},open('Artifacts/SpiderDeityRig/rig_definition.json','w'),indent=2)
print('counts',[(i,int(np.sum(labels==i))) for i in range(9)]);print('weight sums',weights.sum(1).min(),weights.sum(1).max())
fig,axs=plt.subplots(1,3,figsize=(18,9));colors=np.array(['#a1a1aa','#ef4444','#f59e0b','#22c55e','#3b82f6','#e879f9','#facc15','#2dd4bf','#a78bfa'])
for ax,(a,b) in zip(axs,[(0,1),(0,2),(2,1)]):
 ax.scatter(v[:,a],v[:,b],s=.5,c=colors[labels]);
 for k,leg in enumerate(legs):
  p=np.array(leg['points']);ax.plot(p[:,a],p[:,b],'-o',c=colors[k+1],lw=2,markersize=4)
 ax.set_aspect('equal');ax.grid(alpha=.2)
fig.tight_layout();fig.savefig('Artifacts/SpiderDeityRig/weight_regions.png',dpi=150)
