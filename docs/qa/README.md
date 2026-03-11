# QA Reports

该目录用于保存 QA 的测试计划与测试报告。

推荐命名：

- `qa-report-<feature-key>.md`

最低内容要求：

- 测试范围与环境
- 后端 e2e 结果（alert -> analyze）
- 前端浏览器 e2e 结果
- 回归测试结果
- 缺陷与风险评估
- 发布建议（Go / No-Go）

交接要求：

- 在 `docs/13-大型变更任务看板.md` 填写报告链接与结论
- 由项目经理在 `docs/pm/stage-gate-<feature-key>.md` 评审并给出最终 `GO/NO-GO`
