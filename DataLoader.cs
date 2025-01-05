using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TimetableGenerator
{
    public class DataLoader
    {
        public List<List<TimeSlot>> LoadAndGenerateSchedules(out string inputFilePath)
        {
            inputFilePath = null;

            try
            {
                string inputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "input");
                if (!Directory.Exists(inputDir))
                {
                    Directory.CreateDirectory(inputDir);
                    MessageBox.Show(
                        "입력 디렉토리가 생성되었습니다. CSV 파일을 해당 디렉토리에 추가한 후 프로그램을 다시 실행하세요.",
                        "입력 디렉토리 생성됨",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    Environment.Exit(1);
                    return null;
                }

                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    InitialDirectory = inputDir,
                    Filter = "CSV 파일 (*.csv)|*.csv",
                    Title = "시간표 생성용 CSV 파일 선택"
                };

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                {
                    MessageBox.Show(
                        "파일 선택이 취소되었습니다. 프로그램이 종료됩니다.",
                        "파일 선택 취소됨",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    Environment.Exit(1);
                    return null;
                }

                inputFilePath = openFileDialog.FileName;

                if (!File.Exists(inputFilePath))
                {
                    MessageBox.Show(
                        $"선택한 파일이 존재하지 않습니다:\n{inputFilePath}",
                        "파일 오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Environment.Exit(1);
                    return null;
                }

                var lines = File.ReadAllLines(inputFilePath);
                var courses = new List<Course>();

                foreach (var line in lines.Skip(1))
                {
                    var parts = line.Split(',');
                    if (parts.Length < 4)
                    {
                        MessageBox.Show(
                            $"CSV 데이터 형식 오류:\n{line}",
                            "데이터 오류",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        continue;
                    }

                    int courseId = int.Parse(parts[0]);
                    string section = parts[1];
                    string name = parts[2];
                    string[] timeSlots = parts[3].Split('/');

                    courses.Add(new Course
                    {
                        CourseId = courseId,
                        Section = section,
                        Name = name,
                        TimeSlots = timeSlots.ToList()
                    });
                }

                return ScheduleGenerator.GenerateValidSchedules(courses);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "데이터를 처리하는 중 문제가 발생했습니다. 프로그램이 종료됩니다.\n" +
                    $"오류 메시지:\n{ex.Message}",
                    "데이터 처리 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
                return null;
            }
        }
    }
}
