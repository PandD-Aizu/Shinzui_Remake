import numpy as np
from scipy.spatial import cKDTree
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
m=np.load('Artifacts/SpiderDeityRig/mesh.npz');v=m['v'];lab=np.load('Artifacts/SpiderDeityRig/components.npz')['labels']
for h in [.03,.2,.35,.45,.55,.62,.68,.7]:
 p=v[(v[:,0]>.075)&(abs(v[:,1]-h)<.008)&(lab==0)];pairs=cKDTree(p).query_pairs(.028,output_type='ndarray');g=coo_matrix((np.ones(len(pairs)),(pairs[:,0],pairs[:,1])),shape=(len(p),len(p)));n,l=connected_components(g,directed=False)
 print('HEIGHT',h)
 for i in range(n):
  q=p[l==i]
  if len(q)>2:print(len(q),np.round(q.mean(0),3).tolist())
