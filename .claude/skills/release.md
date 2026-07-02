---
name: release
description: 版本发布流程 — 同步版本号文件、生成更新日志、编译验证并 git commit + tag
---

Trigger: `/release` — 版本发布流程。当用户做了几次改动、准备作为新版本发布时调用。

## 行为说明

此 skill 负责同步所有与版本号相关的文件、生成更新日志草稿、并提交 git commit + tag。

## 版本号规则

- **唯一来源**：`WindowsFormsApplication2\Glob.cs` 中 `Glob.Ver` 字段
- **格式**：三位语义化版本 `x.y.z`（如 `1.14.2`）
- **AssemblyInfo 第四位**：始终为 `0`（如 `1.14.2.0`）
- **不影响 TyDll**：`TyDll\Properties\AssemblyInfo.cs` 的版本号独立管理，不纳入此流程

## 需要同步的文件

| 文件 | 操作 |
|------|------|
| `WindowsFormsApplication2\Glob.cs` | 更新 `public static string Ver = "x.y.z"` |
| `WindowsFormsApplication2\Properties\AssemblyInfo.cs` | 更新 `AssemblyVersion` 和 `AssemblyFileVersion` 为 `x.y.z.0` |
| `updates.json` | 在数组最前面插入新版本条目 |

## 执行流程

### 第 1 步：读取当前版本

从 `Glob.cs` 第 22 行读取当前版本号，展示给用户。

### 第 2 步：询问新版本号

询问用户输入新版本号（如 `1.15.0`）。预填当前版本号作为参考。

### 第 3 步：生成更新日志草稿

获取 git tag 并收集 commit 记录：

```bash
# 列出所有 tag 按创建时间排序，取最新的一个
git tag --sort=-creatordate | head -1
```

注意：项目 tag 存在混用格式的问题（最新的 tag 是 `1.14.2`，更早的是 `v1.13.1`）。获取时不要假定前缀。

然后列出该 tag 以来的所有 commit：
```bash
git log <last_tag>..HEAD --oneline --no-merges
```

将 commit 列表整理为更新日志草稿，按 commit 前缀分类：

- `feat` 开头（含 `feat:` 和 `feat(scope):`） → "新功能" 类
- `fix` 开头（含 `fix:` 和 `fix(scope):`） → "问题修复" 类
- 其余 → "功能增强/调整" 类

格式参照 `updates.json` 中的惯例：

```
新功能：
<feature commits 转成中文描述>

问题修复：
<fix commits 转成中文描述>

功能增强/调整：
<其他 commits 转成中文描述>
```

### 第 4 步：用户确认更新日志

将草稿展示给用户，让用户编辑确认。同时询问是否需要填写 `Instra`（更新说明）和 `Other`（其它信息）字段（可选）。

### 第 5 步：同步写入文件

#### 5a. 更新 Glob.cs

将 `public static string Ver = "当前版本"` 替换为 `public static string Ver = "新版本"`。

#### 5b. 更新 AssemblyInfo.cs

将 `AssemblyVersion("当前版本.0")` 替换为 `AssemblyVersion("新版本.0")`
将 `AssemblyFileVersion("当前版本.0")` 替换为 `AssemblyFileVersion("新版本.0")`

#### 5c. 更新 updates.json

在数组第一个元素 `{` 之前插入新条目：

```json
{
  "Version": "新版本",
  "Date": "当天日期 (yyyy-MM-dd)",
  "Instra": "更新说明（用户填写，可为空）",
  "Content": "用户确认的更新日志内容",
  "Other": "其它信息（用户填写，可为空）"
},
```

注意 JSON 数组语法——第一个元素后跟逗号，插入位置在开头的 `[` 换行之后。

### 第 6 步：编译验证

执行 `dotnet build` 确保更新后代码没有问题。

### 第 7 步：Git 提交 + Tag

```bash
git add WindowsFormsApplication2/Glob.cs WindowsFormsApplication2/Properties/AssemblyInfo.cs updates.json
git commit -m "release: v新版本" -m "更新日志摘要（或简要描述）"
git tag -a "v新版本" -m "v新版本 - 日期"
```

全部完成后展示本次发布的摘要（版本号、日期、更新内容要点）。

## 注意事项

- `updates.json` 中旧条目可能混用大小写 `version`/`Version`（历史遗留），新条目统一使用 `Version`。
- Git tag 存在混用格式（`1.14.2` vs `v1.13.1`），新 tag 统一使用 `v` 前缀（如 `v1.15.0`）以匹配大多数历史 tag。
- 更新 `Glob.cs` 时注意只修改 `Ver` 那一行，不要改动其他内容。
- 如果用户中途取消，不写入任何变更。
- 提交前必须通过编译验证，编译失败则不提交。
