using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TimetableGenerator
{
    /// <summary>
    /// CSV 데이터를 로드하고, 유효한 시간표 목록을 생성하는 클래스
    /// </summary>
    public class DataLoader
    {
        /// <summary>
        /// CSV 파일을 로드하고 시간표 데이터를 파싱하여 유효한 시간표 조합을 생성합니다.
        /// </summary>
        /// <param name="inputFilePath">선택된 CSV 파일의 경로</param>
        /// <returns>유효한 시간표 목록</returns>
        public List<List<TimeSlot>> LoadAndGenerateSchedules(out string inputFilePath)
        {
            inputFilePath = null;

            try
            {
                string inputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "input");
                if (!Directory.Exists(inputDir))
                {
                    // 입력 디렉토리가 없는 경우 생성
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
                    // 파일 선택 취소 시 프로그램 종료
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
                    // 파일이 존재하지 않는 경우 에러 메시지 표시 후 종료
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
                    // CSV 데이터의 각 줄을 파싱하여 Course 객체로 변환
                    var parts = line.Split(',');
                    if (parts.Length < 4)
                    {
                        // 데이터 형식 오류 처리
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

                    courses.Add(new Course(courseId, section, name, timeSlots.ToList()));
                }

                // 유효한 시간표 생성
                return ScheduleGenerator.GenerateValidSchedules(courses);
            }
            catch (Exception ex)
            {
                // 데이터 처리 중 오류 발생 시 사용자에게 알림
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