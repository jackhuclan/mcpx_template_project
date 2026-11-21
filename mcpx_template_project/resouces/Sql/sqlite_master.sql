INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'comm_setting', 'comm_setting', 2, 'CREATE TABLE "comm_setting" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_comm_setting" PRIMARY KEY AUTOINCREMENT,
    "name" TEXT NOT NULL,
    "ip" TEXT NOT NULL,
    "port" INTEGER NOT NULL,
    "connect_mode" TEXT NOT NULL,
    "check_cycle_ms" INTEGER NOT NULL,
    "timeout_ms" INTEGER NOT NULL,
    "retry_cycle_s" INTEGER NOT NULL,
    "status" TEXT NOT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sqlite_sequence', 'sqlite_sequence', 3, 'CREATE TABLE sqlite_sequence(name,seq)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'machine', 'machine', 4, 'CREATE TABLE "machine" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_machine" PRIMARY KEY AUTOINCREMENT,
    "comm_setting_id" INTEGER NOT NULL,
    "name" TEXT NOT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'plc_trigger', 'plc_trigger', 5, 'CREATE TABLE "plc_trigger" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_plc_trigger" PRIMARY KEY AUTOINCREMENT,
    "comm_setting_id" INTEGER NOT NULL,
    "device_code" TEXT NOT NULL,
    "address" TEXT NOT NULL,
    "trigger_type" TEXT NOT NULL,
    "trigger_mode" TEXT NOT NULL,
    "action_key" TEXT NULL,
    "alarm_category" INTEGER NOT NULL,
    "report_alarm" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "status" TEXT NOT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'recipe', 'recipe', 6, 'CREATE TABLE "recipe" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_recipe" PRIMARY KEY AUTOINCREMENT,
    "recipe_name" TEXT NOT NULL,
    "upper_lami_tmp_stage1" INTEGER NOT NULL,
    "lower_lami_tmp_stage1" INTEGER NOT NULL,
    "upper_lami_tmp_stage2" INTEGER NOT NULL,
    "lower_lami_tmp_stage2" INTEGER NOT NULL,
    "membrane_width" INTEGER NOT NULL,
    "panel_out_tmp" INTEGER NOT NULL,
    "upper_tacking_tmp" INTEGER NOT NULL,
    "lower_tacking_tmp" INTEGER NOT NULL,
    "drv_speed" REAL NOT NULL,
    "upper_tacking_pressure" REAL NOT NULL,
    "front_cu_pos" REAL NOT NULL,
    "rear_cu_pos" REAL NOT NULL,
    "roller_pressure_stage1" REAL NOT NULL,
    "roller_pressure_stage2" REAL NOT NULL,
    "duration_film" REAL NOT NULL,
    "plate_thickness" REAL NOT NULL,
    "ccode" TEXT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'secs_ceid', 'secs_ceid', 7, 'CREATE TABLE "secs_ceid" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_secs_ceid" PRIMARY KEY AUTOINCREMENT,
    "instance_id" INTEGER NOT NULL,
    "ceid" INTEGER NOT NULL,
    "name" TEXT NOT NULL,
    "status" TEXT NOT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'secs_report', 'secs_report', 8, 'CREATE TABLE "secs_report" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_secs_report" PRIMARY KEY AUTOINCREMENT,
    "instance_id" INTEGER NOT NULL,
    "report_id" INTEGER NOT NULL,
    "name" TEXT NOT NULL,
    "status" TEXT NOT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'secs_sxfy', 'secs_sxfy', 9, 'CREATE TABLE "secs_sxfy" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_secs_sxfy" PRIMARY KEY AUTOINCREMENT,
    "instance_id" INTEGER NOT NULL,
    "stream" INTEGER NOT NULL,
    "function" INTEGER NOT NULL,
    "allow_offline" INTEGER NOT NULL,
    "allow_online_local" INTEGER NOT NULL,
    "allow_online_remote" INTEGER NOT NULL,
    "status" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'secs_vid', 'secs_vid', 11, 'CREATE TABLE "secs_vid" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_secs_vid" PRIMARY KEY AUTOINCREMENT,
    "instance_id" INTEGER NOT NULL,
    "vid_type" TEXT NOT NULL,
    "vid_id" INTEGER NOT NULL,
    "name" TEXT NOT NULL,
    "data_type" TEXT NOT NULL,
    "source" TEXT NOT NULL,
    "plc_address" TEXT NULL,
    "device_code" TEXT NULL,
    "default_value" TEXT NULL,
    "status" TEXT NOT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_dept', 'sys_dept', 13, 'CREATE TABLE "sys_dept" (
    "dept_id" INTEGER NOT NULL CONSTRAINT "PK_sys_dept" PRIMARY KEY AUTOINCREMENT,
    "parent_id" INTEGER NULL,
    "ancestors" TEXT NULL,
    "dept_name" TEXT NOT NULL,
    "order_num" INTEGER NULL,
    "leader" TEXT NULL,
    "phone" TEXT NULL,
    "email" TEXT NULL,
    "status" TEXT NULL,
    "del_flag" TEXT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_dict_data', 'sys_dict_data', 14, 'CREATE TABLE "sys_dict_data" (
    "dict_code" INTEGER NOT NULL CONSTRAINT "PK_sys_dict_data" PRIMARY KEY AUTOINCREMENT,
    "dict_sort" INTEGER NULL,
    "dict_label" TEXT NULL,
    "dict_value" TEXT NULL,
    "dict_type" TEXT NULL,
    "css_class" TEXT NULL,
    "list_class" TEXT NULL,
    "is_default" TEXT NULL,
    "status" TEXT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_menu', 'sys_menu', 15, 'CREATE TABLE "sys_menu" (
    "menu_id" INTEGER NOT NULL CONSTRAINT "PK_sys_menu" PRIMARY KEY AUTOINCREMENT,
    "menu_name" TEXT NOT NULL,
    "parent_id" INTEGER NULL,
    "order_num" INTEGER NULL,
    "component" TEXT NULL,
    "menu_type" TEXT NULL,
    "visible" TEXT NULL,
    "status" TEXT NULL,
    "perms" TEXT NULL,
    "icon" TEXT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_notice', 'sys_notice', 16, 'CREATE TABLE "sys_notice" (
    "notice_id" INTEGER NOT NULL CONSTRAINT "PK_sys_notice" PRIMARY KEY AUTOINCREMENT,
    "notice_title" TEXT NOT NULL,
    "notice_type" TEXT NOT NULL,
    "notice_content" TEXT NULL,
    "source" TEXT NULL,
    "status" TEXT NOT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_notice_read', 'sys_notice_read', 17, 'CREATE TABLE "sys_notice_read" (
    "notice_id" INTEGER NOT NULL,
    "user_id" INTEGER NOT NULL,
    "read_time" TEXT NULL,
    CONSTRAINT "PK_sys_notice_read" PRIMARY KEY ("notice_id", "user_id")
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('index', 'sqlite_autoindex_sys_notice_read_1', 'sys_notice_read', 18, null);
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_oper_log', 'sys_oper_log', 19, 'CREATE TABLE "sys_oper_log" (
    -- 操作日志记录

    -- 日志主键
    "oper_id" INTEGER NOT NULL CONSTRAINT "PK_sys_oper_log" PRIMARY KEY AUTOINCREMENT,

    -- 模块标题
    "title" TEXT NOT NULL,

    -- 业务类型（0其它 1新增 2修改 3删除）
    "business_type" INTEGER NOT NULL,

    -- 方法名称
    "method" TEXT NOT NULL,

    -- 操作类别（0其它 1后台用户 2手机端用户）
    "operator_type" INTEGER NOT NULL,

    -- 操作人员
    "oper_name" TEXT NOT NULL,

    -- 部门名称
    "dept_name" TEXT NOT NULL,

    -- 主机地址
    "oper_ip" TEXT NOT NULL,

    -- 操作地点
    "oper_location" TEXT NOT NULL,

    -- 请求参数
    "oper_param" TEXT NOT NULL,

    -- 返回参数
    "json_result" TEXT NOT NULL,

    -- 操作状态（0正常 1异常）
    "status" INTEGER NOT NULL,

    -- 错误消息
    "error_msg" TEXT NOT NULL,

    -- 操作时间
    "oper_time" TEXT NULL,

    -- 消耗时间
    "cost_time" INTEGER NOT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_post', 'sys_post', 20, 'CREATE TABLE "sys_post" (
    "post_id" INTEGER NOT NULL CONSTRAINT "PK_sys_post" PRIMARY KEY AUTOINCREMENT,
    "post_code" TEXT NOT NULL,
    "post_name" TEXT NOT NULL,
    "post_sort" INTEGER NOT NULL,
    "status" TEXT NOT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_role', 'sys_role', 22, 'CREATE TABLE "sys_role" (
    "role_id" INTEGER NOT NULL CONSTRAINT "PK_sys_role" PRIMARY KEY AUTOINCREMENT,
    "role_name" TEXT NOT NULL,
    "role_key" TEXT NOT NULL,
    "role_sort" INTEGER NOT NULL,
    "data_scope" TEXT NULL,
    "menu_check_strictly" INTEGER NULL,
    "dept_check_strictly" INTEGER NULL,
    "status" TEXT NOT NULL,
    "del_flag" TEXT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_role_menu', 'sys_role_menu', 23, 'CREATE TABLE "sys_role_menu" (
    "role_id" INTEGER NOT NULL,
    "menu_id" INTEGER NOT NULL,
    CONSTRAINT "PK_sys_role_menu" PRIMARY KEY ("role_id", "menu_id")
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('index', 'sqlite_autoindex_sys_role_menu_1', 'sys_role_menu', 24, null);
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_user', 'sys_user', 25, 'CREATE TABLE "sys_user" (
    "user_id" INTEGER NOT NULL CONSTRAINT "PK_sys_user" PRIMARY KEY AUTOINCREMENT,
    "dept_id" INTEGER NULL,
    "user_name" TEXT NOT NULL,
    "nick_name" TEXT NOT NULL,
    "user_type" TEXT NULL,
    "email" TEXT NULL,
    "phonenumber" TEXT NULL,
    "sex" TEXT NULL,
    "avatar" TEXT NULL,
    "password" TEXT NOT NULL,
    "status" TEXT NULL,
    "del_flag" TEXT NULL,
    "login_ip" TEXT NULL,
    "login_date" TEXT NULL,
    "pwd_update_date" TEXT NULL,
    "expire_date" TEXT NULL,
    "last_check_time" TEXT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_user_post', 'sys_user_post', 26, 'CREATE TABLE "sys_user_post" (
    "user_id" INTEGER NOT NULL,
    "post_id" INTEGER NOT NULL,
    CONSTRAINT "PK_sys_user_post" PRIMARY KEY ("user_id", "post_id")
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('index', 'sqlite_autoindex_sys_user_post_1', 'sys_user_post', 27, null);
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'sys_user_role', 'sys_user_role', 28, 'CREATE TABLE "sys_user_role" (
    "user_id" INTEGER NOT NULL,
    "role_id" INTEGER NOT NULL,
    CONSTRAINT "PK_sys_user_role" PRIMARY KEY ("user_id", "role_id")
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('index', 'sqlite_autoindex_sys_user_role_1', 'sys_user_role', 29, null);
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'comm_setting_area', 'comm_setting_area', 30, 'CREATE TABLE "comm_setting_area" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_comm_setting_area" PRIMARY KEY AUTOINCREMENT,
    "setting_id" INTEGER NOT NULL,
    "area_type" TEXT NOT NULL,
    "name" TEXT NULL,
    "position" TEXT NULL,
    "quantity" INTEGER NOT NULL,
    "sort" INTEGER NOT NULL,
    CONSTRAINT "FK_comm_setting_area_comm_setting_setting_id" FOREIGN KEY ("setting_id") REFERENCES "comm_setting" ("id") ON DELETE CASCADE
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'secs_instance', 'secs_instance', 31, 'CREATE TABLE "secs_instance" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_secs_instance" PRIMARY KEY AUTOINCREMENT,
    "name" TEXT NOT NULL,
    "ip_address" TEXT NOT NULL,
    "port" INTEGER NOT NULL,
    "device_id" INTEGER NOT NULL,
    "connect_mode" TEXT NOT NULL,
    "t3" INTEGER NOT NULL,
    "t5" INTEGER NOT NULL,
    "t6" INTEGER NOT NULL,
    "t7" INTEGER NOT NULL,
    "t8" INTEGER NOT NULL,
    "comm_setting_id" INTEGER NULL,
    "status" TEXT NOT NULL,
    "create_by" TEXT NULL,
    "create_time" TEXT NULL,
    "update_by" TEXT NULL,
    "update_time" TEXT NULL,
    "remark" TEXT NULL,
    CONSTRAINT "FK_secs_instance_comm_setting_comm_setting_id" FOREIGN KEY ("comm_setting_id") REFERENCES "comm_setting" ("id") ON DELETE SET NULL
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'secs_ceid_report', 'secs_ceid_report', 32, 'CREATE TABLE "secs_ceid_report" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_secs_ceid_report" PRIMARY KEY AUTOINCREMENT,
    "ceid_id" INTEGER NOT NULL,
    "report_id" INTEGER NOT NULL,
    "sort" INTEGER NOT NULL,
    CONSTRAINT "FK_secs_ceid_report_secs_ceid_ceid_id" FOREIGN KEY ("ceid_id") REFERENCES "secs_ceid" ("id") ON DELETE CASCADE,
    CONSTRAINT "FK_secs_ceid_report_secs_report_report_id" FOREIGN KEY ("report_id") REFERENCES "secs_report" ("id") ON DELETE CASCADE
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('table', 'secs_report_vid', 'secs_report_vid', 34, 'CREATE TABLE "secs_report_vid" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_secs_report_vid" PRIMARY KEY AUTOINCREMENT,
    "report_id" INTEGER NOT NULL,
    "vid_id" INTEGER NOT NULL,
    "sort" INTEGER NOT NULL,
    CONSTRAINT "FK_secs_report_vid_secs_report_report_id" FOREIGN KEY ("report_id") REFERENCES "secs_report" ("id") ON DELETE CASCADE,
    CONSTRAINT "FK_secs_report_vid_secs_vid_vid_id" FOREIGN KEY ("vid_id") REFERENCES "secs_vid" ("id") ON DELETE CASCADE
)');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('index', 'IX_comm_setting_area_setting_id', 'comm_setting_area', 35, 'CREATE INDEX "IX_comm_setting_area_setting_id" ON "comm_setting_area" ("setting_id")');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('index', 'IX_secs_ceid_report_ceid_id', 'secs_ceid_report', 36, 'CREATE INDEX "IX_secs_ceid_report_ceid_id" ON "secs_ceid_report" ("ceid_id")');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('index', 'IX_secs_ceid_report_report_id', 'secs_ceid_report', 37, 'CREATE INDEX "IX_secs_ceid_report_report_id" ON "secs_ceid_report" ("report_id")');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('index', 'IX_secs_instance_comm_setting_id', 'secs_instance', 38, 'CREATE INDEX "IX_secs_instance_comm_setting_id" ON "secs_instance" ("comm_setting_id")');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('index', 'IX_secs_report_vid_report_id', 'secs_report_vid', 39, 'CREATE INDEX "IX_secs_report_vid_report_id" ON "secs_report_vid" ("report_id")');
INSERT INTO sqlite_master (type, name, tbl_name, rootpage, sql) VALUES ('index', 'IX_secs_report_vid_vid_id', 'secs_report_vid', 40, 'CREATE INDEX "IX_secs_report_vid_vid_id" ON "secs_report_vid" ("vid_id")');
