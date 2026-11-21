using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using mcpx_template_project.Domain.Entities.System;

namespace mcpx_template_project.Configation
{
    /// <summary>
    /// 数据库初始化器：使用 EF Core Code First 自动建表 + 种子数据
    /// 不再依赖手动 DDL 脚本执行
    /// </summary>
    /// <author>Rudeus</author>
    public static class DatabaseInitializer
    {
        /// <summary>
        /// 种子数据SQL文件按依赖顺序执行
        /// 系统管理模板种子（sys_xxx）优先，业务种子（comm_*/machine/plc_trigger/recipe）次之，SECS/GEM 种子最后
        /// 注：sqlite_master.sql / sqlite_sequence.sql 为 SQLite 内部元数据导出，不作为种子执行
        /// </summary>
        private static readonly string[] SeedFileOrder =
        {
            // 系统管理模板种子（sys_xxx）
            "sys_dept.sql",
            "sys_dict_data.sql",
            "sys_menu.sql",
            "sys_post.sql",
            "sys_role.sql",
            "sys_role_menu.sql",
            "sys_user.sql",
            "sys_user_post.sql",
            "sys_user_role.sql",
            "sys_notice.sql",

            // 业务配置种子（comm_setting 为基础，其余依赖其 id）
            "comm_setting.sql",
            "comm_setting_area.sql",
            "machine.sql",
            "plc_trigger.sql",
            "recipe.sql",

            // SECS/GEM 种子（instance 优先，report/vid/ceid 依赖 instance_id）
            "secs_instance.sql",
            "secs_ceid.sql",
            "secs_report.sql",
            "secs_vid.sql",
            "secs_report_vid.sql",
            "secs_ceid_report.sql",
            "secs_sxfy.sql"
        };

        /// <summary>
        /// 执行数据库初始化：
        /// 1. EnsureCreated（Code First 按 DbSet/模型 创建表，若表已存在则跳过）
        /// 2. 若无种子数据，则执行初始化 SQL
        /// </summary>
        /// <param name="contextFactory">EF DbContext 工厂</param>
        /// <param name="sqlDirectory">种子数据SQL文件所在目录（相对当前工作目录）</param>
        public static void Initialize(
            IDbContextFactory<EFDbContext> contextFactory,
            string sqlDirectory)
        {
            using var db = contextFactory.CreateDbContext();

            // 1. 基于 EF 模型自动建表
            db.Database.EnsureCreated();

            // 2. 检查是否已有种子数据（以 sys_user 为标志）
            if (db.SysUsers.Any())
                return;

            // 3. 执行SQL种子数据文件（事务包裹，确保原子性）
            string fullSqlDir = System.IO.Path.IsPathRooted(sqlDirectory)
                ? sqlDirectory
                : System.IO.Path.Combine(Directory.GetCurrentDirectory(), sqlDirectory);

            if (!Directory.Exists(fullSqlDir))
                return;

            using var transaction = db.Database.BeginTransaction();
            try
            {
                foreach (var fileName in SeedFileOrder)
                {
                    string filePath = System.IO.Path.Combine(fullSqlDir, fileName);
                    ExecuteSqlFile(db, filePath);
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static void ExecuteSqlFile(EFDbContext db, string filePath)
        {
            if (!File.Exists(filePath))
                return;

            string sql = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(sql))
                return;

            // SQLite 原生支持多语句执行，直接传入完整 SQL 文本
            // 避免按 ; 拆分时误拆分字符串值内部的引号
            db.Database.ExecuteSqlRaw(sql);
        }
    }
}
