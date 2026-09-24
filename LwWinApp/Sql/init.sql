-- =============================================================
--  LwWinApp 演示库初始化脚本
--  目标：你自己的 MySQL 8.0 / 数据库 lwwinapp（连接串在 appsettings.json 里配）
--
--  执行方式：
--    mysql -h 127.0.0.1 -P 3306 -u your-user -p lwwinapp < init.sql
--  或在应用「系统设置」页点「初始化演示数据」。
--
--  脚本可重复执行：建表用 IF NOT EXISTS，插数据用 INSERT IGNORE +
--  固定主键，重复跑不会产生重复数据，也不会删除已有数据。
-- =============================================================

SET NAMES utf8mb4;

-- -------------------------------------------------------------
-- 部门（自引用层级，parent_id = 0 为顶级）
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `sys_dept` (
  `id`          BIGINT       NOT NULL AUTO_INCREMENT,
  `parent_id`   BIGINT       NOT NULL DEFAULT 0,
  `dept_name`   VARCHAR(64)  NOT NULL,
  `sort`        INT          NOT NULL DEFAULT 0,
  `leader`      VARCHAR(32)           DEFAULT NULL,
  `status`      TINYINT      NOT NULL DEFAULT 1,
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_parent_id` (`parent_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='部门';

-- -------------------------------------------------------------
-- 角色
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `sys_role` (
  `id`          BIGINT       NOT NULL AUTO_INCREMENT,
  `role_code`   VARCHAR(64)  NOT NULL,
  `role_name`   VARCHAR(64)  NOT NULL,
  `sort`        INT          NOT NULL DEFAULT 0,
  `status`      TINYINT      NOT NULL DEFAULT 1,
  `remark`      VARCHAR(255)          DEFAULT NULL,
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_role_code` (`role_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='角色';

-- -------------------------------------------------------------
-- 用户
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `sys_user` (
  `id`          BIGINT       NOT NULL AUTO_INCREMENT,
  `user_name`   VARCHAR(64)  NOT NULL,
  `real_name`   VARCHAR(64)  NOT NULL,
  `password`    VARCHAR(160) NOT NULL,
  `email`       VARCHAR(128)          DEFAULT NULL,
  `phone`       VARCHAR(32)           DEFAULT NULL,
  `gender`      TINYINT      NOT NULL DEFAULT 0,
  `dept_id`     BIGINT                DEFAULT NULL,
  `role_id`     BIGINT                DEFAULT NULL,
  `status`      TINYINT      NOT NULL DEFAULT 1,
  `remark`      VARCHAR(255)          DEFAULT NULL,
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` DATETIME              DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_user_name` (`user_name`),
  KEY `idx_dept_id` (`dept_id`),
  KEY `idx_role_id` (`role_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='用户';

-- -------------------------------------------------------------
-- 操作日志（种子数据给到 200 条，用来演示分页）
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `sys_log` (
  `id`          BIGINT       NOT NULL AUTO_INCREMENT,
  `level`       TINYINT      NOT NULL DEFAULT 1,
  `module`      VARCHAR(64)  NOT NULL DEFAULT '',
  `message`     VARCHAR(512) NOT NULL DEFAULT '',
  `user_name`   VARCHAR(64)           DEFAULT NULL,
  `ip`          VARCHAR(64)           DEFAULT NULL,
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_level` (`level`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='操作日志';

-- =============================================================
--  种子数据
-- =============================================================

INSERT IGNORE INTO `sys_dept` (`id`, `parent_id`, `dept_name`, `sort`, `leader`, `status`) VALUES
  (1, 0, '总公司',       1, '李伟',   1),
  (2, 1, '技术中心',     1, '王强',   1),
  (3, 1, '市场部',       2, '赵敏',   1),
  (4, 1, '财务部',       3, '钱芳',   1),
  (5, 2, '研发一组',     1, '孙磊',   1),
  (6, 2, '研发二组',     2, '周涛',   1),
  (7, 2, '测试组',       3, '吴静',   1),
  (8, 3, '华东大区',     1, '郑凯',   1),
  (9, 3, '华北大区',     2, '冯超',   1),
  (10, 4, '结算组',      1, '陈曦',   0);

INSERT IGNORE INTO `sys_role` (`id`, `role_code`, `role_name`, `sort`, `status`, `remark`) VALUES
  (1, 'admin',    '超级管理员', 1, 1, '拥有系统全部权限'),
  (2, 'sysadmin', '系统管理员', 2, 1, '负责用户、角色、部门维护'),
  (3, 'operator', '业务操作员', 3, 1, '日常业务数据的录入与查询'),
  (4, 'auditor',  '审计员',     4, 1, '只读权限，可查看操作日志'),
  (5, 'guest',    '访客',       5, 0, '仅可查看公开数据，已停用');

-- 演示账号统一密码 123456。
-- 存储格式为 SHA256(明文 + 盐) 的小写十六进制，盐见 appsettings.json 的 App:PasswordSalt。
INSERT IGNORE INTO `sys_user`
  (`id`, `user_name`, `real_name`, `password`, `email`, `phone`, `gender`, `dept_id`, `role_id`, `status`, `remark`) VALUES
  ( 1, 'admin',    '李伟',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'admin@lwwinapp.com',    '13800000001', 1, 1, 1, 1, '系统内置超级管理员'),
  ( 2, 'sysadmin', '王强',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'wangqiang@lwwinapp.com','13800000002', 1, 2, 2, 1, NULL),
  ( 3, 'zhangsan', '张三',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'zhangsan@lwwinapp.com', '13800000003', 1, 5, 3, 1, '研发一组负责人'),
  ( 4, 'lisi',     '李四',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'lisi@lwwinapp.com',     '13800000004', 1, 5, 3, 1, NULL),
  ( 5, 'wangwu',   '王五',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'wangwu@lwwinapp.com',   '13800000005', 1, 6, 3, 1, NULL),
  ( 6, 'zhaoliu',  '赵六',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'zhaoliu@lwwinapp.com',  '13800000006', 2, 6, 3, 1, NULL),
  ( 7, 'sunqi',    '孙七',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'sunqi@lwwinapp.com',    '13800000007', 2, 7, 3, 1, NULL),
  ( 8, 'zhouba',   '周八',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'zhouba@lwwinapp.com',   '13800000008', 1, 7, 3, 0, '已离职，账号停用'),
  ( 9, 'wujiu',    '吴九',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'wujiu@lwwinapp.com',    '13800000009', 2, 5, 3, 1, NULL),
  (10, 'zhengshi', '郑十',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'zhengshi@lwwinapp.com', '13800000010', 1, 8, 3, 1, '华东大区负责人'),
  (11, 'fengcy',   '冯超',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'fengchao@lwwinapp.com', '13800000011', 1, 9, 3, 1, NULL),
  (12, 'chenxi',   '陈曦',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'chenxi@lwwinapp.com',   '13800000012', 2, 10, 4, 1, NULL),
  (13, 'qianfang', '钱芳',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'qianfang@lwwinapp.com', '13800000013', 2, 4, 2, 1, '财务负责人'),
  (14, 'zhaomin',  '赵敏',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'zhaomin@lwwinapp.com',  '13800000014', 2, 3, 2, 1, '市场部负责人'),
  (15, 'huangyi',  '黄一',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'huangyi@lwwinapp.com',  '13800000015', 1, 8, 4, 1, NULL),
  (16, 'linber',   '林二',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'linber@lwwinapp.com',   '13800000016', 2, 9, 4, 1, NULL),
  (17, 'hesan',    '何三',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'hesan@lwwinapp.com',    '13800000017', 1, 5, 4, 1, NULL),
  (18, 'guosi',    '郭四',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'guosi@lwwinapp.com',    '13800000018', 2, 6, 3, 1, NULL),
  (19, 'luowu',    '罗五',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'luowu@lwwinapp.com',    '13800000019', 1, 7, 3, 1, NULL),
  (20, 'gaoliu',   '高六',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'gaoliu@lwwinapp.com',   '13800000020', 2, 10, 4, 0, '停用待复核'),
  (21, 'xiaoqi',   '肖七',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'xiaoqi@lwwinapp.com',   '13800000021', 1, 8, 5, 0, '访客账号，已停用'),
  (22, 'tangba',   '唐八',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'tangba@lwwinapp.com',   '13800000022', 2, 3, 3, 1, NULL),
  (23, 'yanjiu',   '严九',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'yanjiu@lwwinapp.com',   '13800000023', 1, 2, 2, 1, NULL),
  (24, 'shenshi',  '沈十',   SHA2(CONCAT('123456', 'LwWinApp@2026'), 256), 'shenshi@lwwinapp.com',  '13800000024', 2, 4, 3, 1, NULL);

-- 把种子用户的创建时间铺开。
-- 否则 24 条记录的 create_time 会全部相同，按时间排序时分页顺序不稳定，
-- 翻页可能重复或漏记录，看起来像 bug。只影响 id <= 24 的种子数据，
-- 应用里新建的用户 id 更大，不会被覆盖。
UPDATE `sys_user` SET `create_time` = DATE_SUB(NOW(), INTERVAL `id` * 7 DAY) WHERE `id` <= 24;

-- 生成 200 条日志，覆盖全部级别与多个模块，时间往前铺开以便按时间段筛选。
INSERT IGNORE INTO `sys_log` (`id`, `level`, `module`, `message`, `user_name`, `ip`, `create_time`)
WITH RECURSIVE `seq` (`n`) AS (
  SELECT 1
  UNION ALL
  SELECT `n` + 1 FROM `seq` WHERE `n` < 200
)
SELECT
  `n`,
  (`n` % 5),
  ELT(1 + (`n` % 6), '用户管理', '角色管理', '部门管理', '系统设置', '数据导出', '登录认证'),
  CONCAT('演示日志 #', `n`, '：',
         ELT(1 + (`n` % 5), '操作成功', '参数校验通过', '查询未命中任何记录',
             '检测到重复提交，已忽略', '请求处理耗时较长，建议检查索引')),
  ELT(1 + (`n` % 6), 'admin', 'zhangsan', 'lisi', 'wangwu', 'sysadmin', 'zhaoliu'),
  CONCAT('192.168.1.', 1 + (`n` % 254)),
  DATE_SUB(NOW(), INTERVAL `n` * 37 MINUTE)
FROM `seq`;

-- 校验：应输出 10 / 5 / 24 / 200
SELECT '部门' AS 表, COUNT(1) AS 记录数 FROM `sys_dept`
UNION ALL SELECT '角色', COUNT(1) FROM `sys_role`
UNION ALL SELECT '用户', COUNT(1) FROM `sys_user`
UNION ALL SELECT '日志', COUNT(1) FROM `sys_log`;
