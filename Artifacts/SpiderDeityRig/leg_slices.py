import numpy as np,matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
m=np.load('Artifacts/SpiderDeityRig/mesh.npz');v=m['v'];f=m['f']; lab=np.load('Artifacts/SpiderDeityRig/components.npz')['labels']
fig,axs=plt.subplots(1,4,figsize=(18,9))
for ax,(zlo,zhi) in zip(axs,[(.15,.43),(.10,.35),(-.17,.07),(-.4,-.12)]):
 mask=(v[:,0]>.06)&(v[:,2]>zlo)&(v[:,2]<zhi)&(lab==0); p=v[mask];sc=ax.scatter(p[:,2],p[:,1],c=p[:,0],s=3,cmap='turbo',vmin=.06,vmax=.41); ax.set_aspect('equal');ax.grid();ax.set_title(str((zlo,zhi)));ax.set_xlabel('Z');ax.set_ylabel('Y')
fig.colorbar(sc,ax=axs);fig.savefig('Artifacts/SpiderDeityRig/leg_slices.png',dpi=160)
