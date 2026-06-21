# 联网摘要

> **Layer**：Foundation

> 所属系统：[`Foundation-联机系统设计.md`](Foundation-联机系统设计.md)
> 说明：本文件保留早期联机方向的简要摘要，详细方案以 [`Foundation-网络方案.md`](Foundation-网络方案.md) 为准。

## 当前方向

- 游戏采用多人合作房间制，由房主创建房间，其他玩家加入。
- 房主作为权威结算端，负责校验和同步关键状态。
- 客户端主要发送操作请求，并根据服务器广播播放表现。

## 文档关系

- 系统边界与同步职责见 [`Foundation-联机系统设计.md`](Foundation-联机系统设计.md)。
- 详细技术选型与开发方案见 [`Foundation-网络方案.md`](Foundation-网络方案.md)。
