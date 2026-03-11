# Architecture Docs

该目录用于保存架构师产出的技术设计文档。

推荐命名：

- `arch-<feature-key>.md`

最低内容要求：

- 需求映射（对应 stories）
- 模块拆分与职责
- API 设计说明（引用 `docs/api/contracts/openapi.yaml`）
- 关键时序/数据流
- 风险与回滚方案

交接要求：

- 在 `docs/13-大型变更任务看板.md` 填写文档链接
- 由项目经理在 `docs/pm/stage-gate-<feature-key>.md` 评审并给出 `GO/NO-GO`
