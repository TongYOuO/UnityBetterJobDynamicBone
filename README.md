
自己改了一版DynamicBoneJob的多线程版本，总结吸收了DynamicBone和MagicaCloth的优劣（具体来说是基于最新版的DynamicBone算法支持了Burst和Jobs多线程优化，同时结合了MagicaCloth的这一套PlayerLoopSystem架构在CompleteJob时减少等待时间，配置上也进行了分类和支持模板配置导入导出更清晰！），差不多两周把主体流程跑通了，有一些表现和功能的问题，稳定性还比较差，正在完善当中┭┮﹏┭┮，作为MagicaCloth的低配版本，整体用的MagicaCloth的框架但是采用了DynamicBone的算法
![](https://tongpic-1312274798.cos.ap-guangzhou.myqcloud.com/Video.mp4)

相比原生的DynamicBone有哪些优势,多了哪些东西？
1. 增加PlayerLoopSystem可以更精确的调控生命周期,在Complete的时候能够保证尽量少在主线程等待,多干一会别的事情
2. 区分Team,ParticleTree,Particle的概念,每个Unity的脚本作为一个Team,每个Team下可以挂多个rootBone,每个RootBone作为一个ParticleTree,RootBone其下的所有骨骼每个骨骼作为一个Particle
3. 统一的Manager管理,扩展性极高(直接再去抄点就变成了magicaCloth,借鉴学习bush),下分ClothMgr,TeamMgr,SimulationMgr,ColliderMgr对标MagicaCloth的系统架构,但是里面的算法却是DynamicBone的算法不用计算ProxyMesh
4. 支持模板化的配置变量,减少人工带来的风险,同时进行了变量分类更清晰,自己写的EditorInspector扩展性也极高,后面如果继续深挖可以接Odin
5. ExNativeList支持可复用的NativeArray,不必每次新增都重新扩容,单独抽出DataChunk的概念记录每部分的开始下标和数据长度,真正做到可运行时卸载,重新拉起时复用之前NativeList卸载掉的下标中的数据
6. 全流程的Job和Burst处理,绝大部分Job做到逐个骨骼并行运算,个别需要用到父骨骼数据进行刚度和弹性计算的逐根骨骼即ParticleTree并行
![Pasted image 20240130171934](https://tongpic-1312274798.cos.ap-guangzhou.myqcloud.com/Pasted%20image%2020240130171934.png)
![Pasted image 20240130172000](https://tongpic-1312274798.cos.ap-guangzhou.myqcloud.com/Pasted%20image%2020240130172000.png)

- 横向对比：
	- 相比于21年Githuh上的Job版本，支持了配置模板化以及变量分类配置更清晰，性能更优，支持运行时卸载模型后重新拉起时NativeArray动态重用之前的结构体不必重新扩容，将支持碰撞处理、支持DistanceDisable
	- 相比于最新版的官方DynamicBone，支持了配置模板化以及变量分类配置更清晰，性能更优

- 性能相比于全部跑在主线程的版本性能耗时降幅70%

