using System.Drawing;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.Style;
using System.Drawing.Text;
using System.Runtime.Intrinsics.X86;
using OfficeOpenXml.Drawing;
using Serilog;

namespace JmsUserLogonAudit
{
    internal class Excel
    {
        internal static void OutXls(FullStatistic fs, FileInfo fileInfo)
        {
            if (fs.userCount == 0)
            {
                fileInfo.Create().Close();
                Log.Warning($"此月份登录记录为空，生成空白Excel占位文件{fileInfo.FullName}");
            }
            else
            {            
                //var path = Path.Combine(Directory.GetCurrentDirectory(), backupOption.localExportPath);
                var users = fs.userStats;
                var fails = fs.failStats;
                var title = fs.year + "-" + fs.month;

                users = users.OrderBy(u => u.totalCount).ToList();
                fails = fails.OrderBy(f => f.count).ToList();
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add(title);
                    //worksheet.Cells.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;//水平居中，全局
                    //worksheet.Cells.Style.VerticalAlignment = ExcelVerticalAlignment.Center;//垂直居中，全局
                    //worksheet.Cells.AutoFitColumns();//全局
                    //worksheet.Cells.Style.WrapText = true;//自动换行,全局
                    worksheet.Cells.Style.Font.Name = "微软雅黑"; //全局

                    // 写入表头
                    worksheet.Cells[2, 1, 2, 3].Merge = true;
                    worksheet.Cells[2, 1, 2, 3].Value = "用户登录数据统计";
                    worksheet.Cells[2, 1, 2, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[2, 18, 2, 19].Merge = true;
                    worksheet.Cells[2, 18, 2, 19].Value = "登录失败数据统计";
                    worksheet.Cells[2, 18, 2, 19].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[3, 1].Value = "用户名";
                    worksheet.Cells[3, 2].Value = "登录计数";
                    worksheet.Cells[3, 3].Value = "失败计数";
                    worksheet.Cells[3, 18].Value = "失败原因";
                    worksheet.Cells[3, 19].Value = "计数";
                    //worksheet.Cells[1, 3].Style.Font.Bold = true;
                    worksheet.Cells[2, 1, 2, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[2, 1, 2, 3].Style.Fill.BackgroundColor.SetColor(Color.Aqua);
                    worksheet.Cells[2, 18, 2, 19].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[2, 18, 2, 19].Style.Fill.BackgroundColor.SetColor(Color.Aqua);
                    worksheet.Cells[3, 1, 3, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[3, 1, 3, 3].Style.Fill.BackgroundColor.SetColor(Color.Khaki);
                    worksheet.Cells[3, 18, 3, 19].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[3, 18, 3, 19].Style.Fill.BackgroundColor.SetColor(Color.Khaki);

                    // 设置列宽
                    worksheet.Column(1).Width = 16;
                    worksheet.Column(2).Width = 12;
                    worksheet.Column(3).Width = 12;
                    worksheet.Column(18).Width = 40;
                    worksheet.Column(19).Width = 8;

                    // 写入数据行
                    int row1 = 4; // 数据从第二行开始
                    foreach (var user in users)
                    {
                        worksheet.Cells[row1, 1].Value = user.username;
                        worksheet.Cells[row1, 2].Value = user.totalCount;
                        worksheet.Cells[row1, 3].Value = user.failCount;
                        worksheet.Cells[row1, 1, row1, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row1, 1, row1, 3].Style.Fill.BackgroundColor.SetColor(Color.Azure);
                        if (user.failCount != 0)
                        {
                            worksheet.Cells[row1, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            worksheet.Cells[row1, 3].Style.Fill.BackgroundColor.SetColor(Color.Red);
                        }

                        row1++; // 每次迭代后递增行号
                    }

                    // 写入总计
                    worksheet.Cells[row1, 1].Value = $"总计{fs.userCount}人"; // 在第一列写入"Total"
                    worksheet.Cells[row1, 2].Value = fs.logonCount; // 写入总计登录次数
                    worksheet.Cells[row1, 3].Value = fs.failCount; // 写入总计失败次数

                    // 给总计行加上黄色背景
                    worksheet.Cells[row1, 1, row1, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row1, 1, row1, 3].Style.Fill.BackgroundColor.SetColor(Color.Yellow);

                    //统计
                    row1 += 2;
                    worksheet.Cells[row1, 1, row1 + 2, 3].Merge = true;
                    worksheet.Cells[row1, 1, row1 + 2, 3].Value = $"本月堡垒机操作用户{fs.userCount}人，会话次数{fs.logonCount}次。" +
                                                                  $"其中有登陆失败记录用户{fs.userStats.Count(x => x.failCount != 0).ToString()}人，次数{fs.failCount}次，原因{fs.failStats.Count}种。";

                    // 给总计行加上黄色背景
                    worksheet.Cells[row1, 1, row1 + 2, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row1, 1, row1 + 2, 3].Style.Fill.BackgroundColor
                        .SetColor(Color.CornflowerBlue);
                    worksheet.Cells[row1, 1, row1 + 2, 3].Style.WrapText = true;


                    int row2 = 4;
                    foreach (var fail in fails)
                    {
                        worksheet.Cells[row2, 18].Value = fail.reason;
                        worksheet.Cells[row2, 19].Value = fail.count;
                        row2++; // 每次迭代后递增行号
                    }

                    // 计算总计

                    // 写入总计
                    worksheet.Cells[row2, 18].Value = $"总计{fs.failStats.Count}种"; // 在第一列写入"Total"
                    worksheet.Cells[row2, 19].Value = fs.failCount; // 写入总计登录次数

                    // 给总计行加上黄色背景
                    worksheet.Cells[row2, 18, row2, 19].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row2, 18, row2, 19].Style.Fill.BackgroundColor.SetColor(Color.Yellow);

                    // 创建图表
                    //var chart = worksheet.Drawings.AddChart("MyChart", eChartType.ColumnStacked);
                    var chart = worksheet.Drawings.AddChart("Chart1", eChartType.ColumnClustered3D) as ExcelBarChart;

                    // 设置图表
                    chart.SetPosition(1, 0, 4, 0); // 设置图表位置
                    chart.SetSize(800, 400); // 设置图表大小
                    chart.Style = eChartStyle.Style2;
                    chart.Title.Text = title;
                    chart.Title.Font.Size = 15;
                    chart.Title.Font.Bold = true;
                    chart.XAxis.Title.Text = "登录用户";
                    chart.YAxis.Title.Text = "登录次数";
                    chart.YAxis.Title.Rotation = 270;
                    chart.VaryColors = true;
                    chart.DataLabel.ShowValue = true;
                    chart.DataLabel.ShowCategory = false;
                    chart.DataLabel.ShowLeaderLines = false;
                    chart.Legend.Remove();


                    // 设置图表数据源
                    chart.Series.Add(worksheet.Cells["B4:B" + (users.Count + 3)],
                        worksheet.Cells["A4:A" + (users.Count + 3)]);

                    // 保存到文件
                    //var fullPath = Path.Combine(path, fileName);
                    //var fileInfo = new FileInfo(fullPath);
                    //var fileInfo = new FileInfo(Path.Combine(path, fileInfo1.Name)); 
                    try
                    {
                        package.SaveAs(fileInfo);
                        //Base.PrintColoredString(fileInfo.FullName, ConsoleColor.Yellow);
                        Log.Information($"路径{fileInfo.FullName}");
                    }
                    catch (Exception e)
                    {
                        //Base.PrintColoredString(e.Message, ConsoleColor.Red);
                        Log.Error($"导出失败：{e.Message}");
                        throw;
                    }
                }
            }
        }
    }
}

