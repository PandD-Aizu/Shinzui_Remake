import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
m=np.load('Artifacts/SpiderDeityRig/mesh.npz');v=m['v'];f=m['f'];labs=np.load('Artifacts/SpiderDeityRig/components.npz')['labels']; sel=np.all(labs[f]==0,axis=1);f=f[sel]
fig,axs=plt.subplots(1,3,figsize=(18,9))
for ax,(a,c) in zip(axs,[(0,1),(0,2),(2,1)]):
 ax.triplot(v[:,a],v[:,c],f,color='#444444',lw=.3);ax.set_aspect('equal');ax.set_xlabel('XYZ'[a]);ax.set_ylabel('XYZ'[c]);ax.set_xticks(np.arange(-.4,.5,.05));ax.set_yticks(np.arange(-.4 if c==2 else 0, .5 if c==2 else 1.01,.05));ax.grid(alpha=.4)
fig.tight_layout();fig.savefig('Artifacts/SpiderDeityRig/core_views.png',dpi=180)
