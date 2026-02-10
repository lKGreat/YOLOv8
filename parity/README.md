# YOLOv8 Parity Artifacts

本目录用于保存 C# 与 Python 基线对齐所需的工件。

## baseline.lock.json

- 由 `YOLO.App` 命令生成：
  - `dotnet run --project csharp/src/YOLO.App -- parity-freeze --root d:\Code\YOLOv8`
- 内容包含：
  - 关键 Python 文件哈希
  - 全量 `ultralytics/*.py` 聚合哈希
  - 任务能力探测（Detect/Segment/Pose/Classify/OBB）

## 校验

- 使用命令校验当前工作区是否与锁定基线一致：
  - `dotnet run --project csharp/src/YOLO.App -- parity-report --lock d:\Code\YOLOv8\csharp\parity\baseline.lock.json`

