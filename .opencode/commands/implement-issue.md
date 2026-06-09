---
description: Implement a GitHub issue by number
agent: manager
subtask: false
---
Implement the fixes or features described in GitHub issue [#$ARGUMENTS](https://github.com/PaulTrampert/PrivatePacmanRepo/issues/$ARGUMENTS).

The workflow for implementing a solution to an issue is as follows. For each step, you must pass the relevant findings and results from all previous steps into the prompt of the next `task` call to maintain context:
* Use `task` with `subagent_type: explore` to understand requirements and research the codebase, identifying relevant files and any implicit or missed requirements.
* Use `task` with `subagent_type: general` to formulate an implementation plan based on findings, including necessary test cases.
* Use `task` with `subagent_type: general` to implement the plan in a new branch, then commit and push the changes and create a Pull Request.

When performing the above, ensure your work adheres to CONTRIBUTING.md. Use AGENTS.md to understand the tools available to you, in addition to your system prompt.