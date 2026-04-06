using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Text;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        // Переменные для отслеживания текущего файла и изменений
        private string currentFilePath = null;
        private bool isTextChanged = false;
        private Stack<string> undoStack = new Stack<string>();
        private Stack<string> redoStack = new Stack<string>();
        private bool ignoreTextChanged = false;
        private string lastTextState = "";
        private DataGridView dataGridViewTokens;

        public Form1()
        {
            InitializeComponent();
            InitializeInterface();
        }

        // КЛАСС ДЛЯ ХРАНЕНИЯ ИНФОРМАЦИИ ОБ ОШИБКЕ
        public class CompilerError
        {
            public int Line { get; set; }
            public int Column { get; set; }
            public string Message { get; set; }
        }

        private void InitializeInterface()
        {
            // Настройка заголовка окна
            this.Text = "Лексический анализатор Pascal";

            // Настройка области редактирования
            editorTextBox.TextChanged += EditorTextBox_TextChanged;
            editorTextBox.Dock = DockStyle.Fill;
            editorTextBox.ScrollBars = RichTextBoxScrollBars.Both;
            editorTextBox.DetectUrls = false;
            editorTextBox.MaxLength = int.MaxValue;
            editorTextBox.KeyDown += EditorTextBox_KeyDown;
            editorTextBox.Font = new Font("Consolas", 10);

            // Сохраняем начальное состояние
            lastTextState = editorTextBox.Text;

            // Настройка меню и панели инструментов
            UpdateWindowTitle();

            // СОЗДАЕМ DataGridView НА ВСЮ ПРАВУЮ ПАНЕЛЬ
            dataGridViewTokens = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, // Изменили на None
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White
            };

            // Добавляем колонки
            dataGridViewTokens.Columns.Add("Code", "Код");
            dataGridViewTokens.Columns.Add("Type", "Тип");
            dataGridViewTokens.Columns.Add("Lexeme", "Лексема");
            dataGridViewTokens.Columns.Add("Line", "Строка");
            dataGridViewTokens.Columns.Add("Column", "Колонка");
            dataGridViewTokens.Columns.Add("Error", "Ошибка");

            // НАСТРОЙКА АВТОМАТИЧЕСКОЙ ШИРИНЫ КОЛОНОК
            dataGridViewTokens.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells; // Авторазмер по содержимому
            dataGridViewTokens.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);

            // Дополнительно: растягивать колонку Error, если осталось место
            dataGridViewTokens.Columns["Error"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // Минимальная ширина для каждой колонки
            dataGridViewTokens.Columns["Code"].MinimumWidth = 50;
            dataGridViewTokens.Columns["Type"].MinimumWidth = 80;
            dataGridViewTokens.Columns["Lexeme"].MinimumWidth = 150;
            dataGridViewTokens.Columns["Line"].MinimumWidth = 60;
            dataGridViewTokens.Columns["Column"].MinimumWidth = 60;
            dataGridViewTokens.Columns["Error"].MinimumWidth = 200;

            // Очищаем правую панель и добавляем DataGridView
            splitContainer1.Panel2.Controls.Clear();
            splitContainer1.Panel2.Controls.Add(dataGridViewTokens);

            // Добавляем обработчики
            dataGridViewTokens.CellClick += DataGridViewTokens_CellClick;
            dataGridViewTokens.CellFormatting += DataGridViewTokens_CellFormatting;

            // Добавляем обработчик изменения размера формы
            this.Resize += Form1_Resize;

            // Скрываем outputRichTextBox (он больше не нужен)
            outputRichTextBox.Visible = false;
        }

        // Добавьте этот метод для обновления ширины колонок при изменении размера формы
        private void Form1_Resize(object sender, EventArgs e)
        {
            if (dataGridViewTokens != null)
            {
                // Пересчитываем ширину колонок
                dataGridViewTokens.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            }
        }

        // Также добавьте этот метод для автоматического изменения ширины после добавления строк
        private void AutoResizeDataGridView()
        {
            if (dataGridViewTokens.InvokeRequired)
            {
                dataGridViewTokens.Invoke(new MethodInvoker(AutoResizeDataGridView));
                return;
            }

            dataGridViewTokens.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
        }

        private void DataGridViewTokens_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Подсвечиваем строки с ошибками
            if (dataGridViewTokens.Rows[e.RowIndex].Cells["Error"].Value != null)
            {
                string error = dataGridViewTokens.Rows[e.RowIndex].Cells["Error"].Value.ToString();
                if (!string.IsNullOrEmpty(error))
                {
                    e.CellStyle.BackColor = Color.LightCoral;
                    e.CellStyle.ForeColor = Color.DarkRed;
                }
            }
        }

        private void DataGridViewTokens_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            int line = (int)dataGridViewTokens.Rows[e.RowIndex].Cells["Line"].Value;
            int column = (int)dataGridViewTokens.Rows[e.RowIndex].Cells["Column"].Value;

            if (line > 0 && line <= editorTextBox.Lines.Length)
            {
                int charPos = editorTextBox.GetFirstCharIndexFromLine(line - 1);
                charPos += Math.Max(0, column - 1);

                editorTextBox.SelectionStart = charPos;
                editorTextBox.SelectionLength = 1;
                editorTextBox.ScrollToCaret();
                editorTextBox.Focus();

                // Подсвечиваем позицию
                HighlightErrorPosition(line, column);
            }
        }

        private void HighlightErrorPosition(int line, int column)
        {
            if (line <= 0 || line > editorTextBox.Lines.Length) return;

            int charPos = editorTextBox.GetFirstCharIndexFromLine(line - 1);
            charPos += Math.Max(0, column - 1);

            int oldStart = editorTextBox.SelectionStart;
            int oldLength = editorTextBox.SelectionLength;
            Color oldColor = editorTextBox.SelectionBackColor;

            editorTextBox.SelectionStart = charPos;
            editorTextBox.SelectionLength = 1;
            editorTextBox.SelectionBackColor = Color.LightYellow;

            Timer timer = new Timer();
            timer.Interval = 500;
            timer.Tick += (s, args) =>
            {
                editorTextBox.SelectionStart = charPos;
                editorTextBox.SelectionLength = 1;
                editorTextBox.SelectionBackColor = oldColor;
                editorTextBox.SelectionStart = oldStart;
                editorTextBox.SelectionLength = oldLength;
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }

        // Обработчик нажатия клавиш
        private void EditorTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z)
            {
                Undo();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                Redo();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.Z)
            {
                Redo();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        private void EditorTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!ignoreTextChanged)
            {
                if (lastTextState != editorTextBox.Text)
                {
                    undoStack.Push(lastTextState);
                    lastTextState = editorTextBox.Text;
                    redoStack.Clear();
                }
            }
            isTextChanged = true;
            UpdateWindowTitle();
            HighlightSyntax();
        }

        private void Undo()
        {
            if (undoStack.Count > 0)
            {
                ignoreTextChanged = true;
                redoStack.Push(editorTextBox.Text);
                string previousText = undoStack.Pop();
                editorTextBox.Text = previousText;
                lastTextState = previousText;
                editorTextBox.SelectionStart = editorTextBox.TextLength;
                editorTextBox.ScrollToCaret();
                ignoreTextChanged = false;
                HighlightSyntax();
            }
        }

        private void Redo()
        {
            if (redoStack.Count > 0)
            {
                ignoreTextChanged = true;
                undoStack.Push(editorTextBox.Text);
                string nextText = redoStack.Pop();
                editorTextBox.Text = nextText;
                lastTextState = nextText;
                editorTextBox.SelectionStart = editorTextBox.TextLength;
                editorTextBox.ScrollToCaret();
                ignoreTextChanged = false;
                HighlightSyntax();
            }
        }

        private void создатьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateNewFile();
        }

        private void открытьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFile();
        }

        private void сохранитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFile();
        }

        private void сохранитьКакToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileAs();
        }

        private void выходToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void отменитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Undo();
        }

        private void повторитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Redo();
        }

        private void вырезатьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            editorTextBox.Cut();
        }

        private void копироватьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            editorTextBox.Copy();
        }

        private void вставитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            editorTextBox.Paste();
        }

        private void удалитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(editorTextBox.SelectedText))
                editorTextBox.SelectedText = "";
        }

        private void выделитьВсеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            editorTextBox.SelectAll();
        }

        private void пускToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dataGridViewTokens.Rows.Clear();

            string sourceCode = editorTextBox.Text;

            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                // Добавляем строку с ошибкой в DataGridView
                dataGridViewTokens.Rows.Add(0, "ERROR", "Нет текста для анализа", 0, 0, "Пустой файл");
                return;
            }

            AnalyzePascalCode(sourceCode);
        }

        private void вызовСправкиToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowHelp();
        }

        private void оПрограммеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAbout();
        }

        private void toolStripButtonNew_Click(object sender, EventArgs e)
        {
            CreateNewFile();
        }

        private void toolStripButtonOpen_Click(object sender, EventArgs e)
        {
            OpenFile();
        }

        private void toolStripButtonSave_Click(object sender, EventArgs e)
        {
            SaveFile();
        }

        private void toolStripButtonUndo_Click(object sender, EventArgs e)
        {
            отменитьToolStripMenuItem_Click(sender, e);
        }

        private void toolStripButtonRedo_Click(object sender, EventArgs e)
        {
            повторитьToolStripMenuItem_Click(sender, e);
        }

        private void toolStripButtonCut_Click(object sender, EventArgs e)
        {
            вырезатьToolStripMenuItem_Click(sender, e);
        }

        private void toolStripButtonCopy_Click(object sender, EventArgs e)
        {
            копироватьToolStripMenuItem_Click(sender, e);
        }

        private void toolStripButtonPaste_Click(object sender, EventArgs e)
        {
            вставитьToolStripMenuItem_Click(sender, e);
        }

        private void toolStripButtonRun_Click(object sender, EventArgs e)
        {
            пускToolStripMenuItem_Click(sender, e);
        }

        private void toolStripButtonHelp_Click(object sender, EventArgs e)
        {
            ShowHelp();
        }

        private void toolStripButtonAbout_Click(object sender, EventArgs e)
        {
            ShowAbout();
        }

        private void CreateNewFile()
        {
            if (CheckUnsavedChanges())
            {
                editorTextBox.Clear();
                currentFilePath = null;
                isTextChanged = false;
                UpdateWindowTitle();
                dataGridViewTokens.Rows.Clear();
            }
        }

        private void OpenFile()
        {
            if (!CheckUnsavedChanges())
                return;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Pascal файлы (*.pas)|*.pas|Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string filePath = openFileDialog.FileName;
                        string fileContent = File.ReadAllText(filePath, Encoding.UTF8);
                        editorTextBox.Text = fileContent;
                        currentFilePath = filePath;
                        isTextChanged = false;
                        UpdateWindowTitle();
                        dataGridViewTokens.Rows.Clear();

                        // Добавляем информационную строку
                        dataGridViewTokens.Rows.Add(0, "INFO", $"Файл открыт: {Path.GetFileName(filePath)}", 0, 0, "");
                        dataGridViewTokens.Rows.Add(0, "INFO", $"Размер: {fileContent.Length} символов", 0, 0, "");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при открытии файла: {ex.Message}",
                            "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void SaveFile()
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveFileAs();
            }
            else
            {
                SaveToFile(currentFilePath);
            }
        }

        private void SaveFileAs()
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Pascal файлы (*.pas)|*.pas|Текстовые файлы (*.txt)|*.txt";
                saveFileDialog.FilterIndex = 1;
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.DefaultExt = "pas";
                saveFileDialog.FileName = "program.pas";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = saveFileDialog.FileName;
                    SaveToFile(filePath);
                }
            }
        }

        private void SaveToFile(string filePath)
        {
            try
            {
                File.WriteAllText(filePath, editorTextBox.Text, Encoding.UTF8);
                currentFilePath = filePath;
                isTextChanged = false;
                UpdateWindowTitle();

                dataGridViewTokens.Rows.Clear();
                dataGridViewTokens.Rows.Add(0, "INFO", $"Файл сохранен: {Path.GetFileName(filePath)}", 0, 0, "");
                dataGridViewTokens.Rows.Add(0, "INFO", $"Время: {DateTime.Now:HH:mm:ss}", 0, 0, "");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool CheckUnsavedChanges()
        {
            if (isTextChanged)
            {
                DialogResult result = MessageBox.Show(
                    "Сохранить изменения в текущем файле?",
                    "Несохраненные изменения",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    SaveFile();
                }
                else if (result == DialogResult.Cancel)
                {
                    return false;
                }
            }
            return true;
        }

        private void ShowHelp()
        {
            string helpText = @"=== СПРАВКА ПО КОМПИЛТОРУ PASCAL ===

Функции меню 'Файл':
• Создать - создание нового документа
• Открыть - открытие существующего файла
• Сохранить - сохранение текущего файла
• Сохранить как - сохранение под новым именем
• Выход - закрытие программы

Функции меню 'Правка':
• Отменить - отмена последнего действия
• Повторить - повтор отмененного действия
• Вырезать - перемещение выделенного текста в буфер
• Копировать - копирование выделенного текста
• Вставить - вставка текста из буфера
• Удалить - удаление выделенного текста
• Выделить все - выделение всего текста

Функции меню 'Пуск':
• Запуск лексического анализатора Pascal

Результаты анализа отображаются в таблице:
• Код - числовой идентификатор типа лексемы
• Тип - тип лексемы (Keyword, Identifier, Number и т.д.)
• Лексема - выделенная подстрока
• Строка - номер строки
• Колонка - позиция в строке
• Ошибка - сообщение об ошибке (если есть)

Недопустимые символы подсвечиваются красным.
Кликните на строке для перехода к позиции символа.";

            MessageBox.Show(helpText, "Справка", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowAbout()
        {
            string aboutText = @"Лексический анализатор Pascal

Версия 1.0

Разработано в рамках курсового проекта.

Приложение предназначено для лексического анализа
программ на языке Pascal с поддержкой записей (record).

© 2026 Все права защищены.";

            MessageBox.Show(aboutText, "О программе",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateWindowTitle()
        {
            string fileName = string.IsNullOrEmpty(currentFilePath) ?
                "Новый файл" : Path.GetFileName(currentFilePath);
            string modified = isTextChanged ? "*" : "";
            this.Text = $"Лексический анализатор Pascal - {fileName}{modified}";
        }

        private void HighlightSyntax()
        {
            int selectionStart = editorTextBox.SelectionStart;
            int selectionLength = editorTextBox.SelectionLength;

            editorTextBox.TextChanged -= EditorTextBox_TextChanged;

            editorTextBox.SelectAll();
            editorTextBox.SelectionColor = Color.Black;
            editorTextBox.SelectionFont = new Font(editorTextBox.Font, FontStyle.Regular);

            // Ключевые слова Pascal
            string[] pascalKeywords = {
                "program", "begin", "end", "var", "const", "type",
                "procedure", "function", "if", "then", "else", "case", "of",
                "while", "do", "for", "to", "downto", "repeat", "until",
                "record", "array", "set", "file", "string", "integer",
                "real", "boolean", "char", "true", "false", "and", "or",
                "not", "div", "mod", "in", "nil", "uses", "interface",
                "implementation", "unit", "object", "class"
            };

            foreach (string keyword in pascalKeywords)
            {
                int index = 0;
                while (index < editorTextBox.TextLength)
                {
                    int wordStart = editorTextBox.Find(keyword, index, RichTextBoxFinds.WholeWord);
                    if (wordStart == -1) break;

                    editorTextBox.SelectionStart = wordStart;
                    editorTextBox.SelectionLength = keyword.Length;
                    editorTextBox.SelectionColor = Color.Blue;
                    editorTextBox.SelectionFont = new Font(editorTextBox.Font, FontStyle.Bold);

                    index = wordStart + keyword.Length;
                }
            }

            // Подсвечиваем комментарии
            HighlightPascalComments();

            editorTextBox.SelectionStart = selectionStart;
            editorTextBox.SelectionLength = selectionLength;
            editorTextBox.SelectionColor = Color.Black;

            editorTextBox.TextChanged += EditorTextBox_TextChanged;
        }

        private void HighlightPascalComments()
        {
            string text = editorTextBox.Text;

            // Комментарии { ... }
            int startPos = 0;
            while (startPos < text.Length)
            {
                int commentStart = text.IndexOf('{', startPos);
                if (commentStart == -1) break;

                int commentEnd = text.IndexOf('}', commentStart + 1);
                if (commentEnd == -1) break;

                editorTextBox.SelectionStart = commentStart;
                editorTextBox.SelectionLength = commentEnd - commentStart + 1;
                editorTextBox.SelectionColor = Color.Green;
                editorTextBox.SelectionFont = new Font(editorTextBox.Font, FontStyle.Italic);

                startPos = commentEnd + 1;
            }

            // Комментарии (* ... *)
            startPos = 0;
            while (startPos < text.Length)
            {
                int commentStart = text.IndexOf("(*", startPos);
                if (commentStart == -1) break;

                int commentEnd = text.IndexOf("*)", commentStart + 2);
                if (commentEnd == -1) break;

                editorTextBox.SelectionStart = commentStart;
                editorTextBox.SelectionLength = commentEnd - commentStart + 2;
                editorTextBox.SelectionColor = Color.Green;
                editorTextBox.SelectionFont = new Font(editorTextBox.Font, FontStyle.Italic);

                startPos = commentEnd + 2;
            }

            // Строки в кавычках
            startPos = 0;
            while (startPos < text.Length)
            {
                int stringStart = text.IndexOf('\'', startPos);
                if (stringStart == -1) break;

                int stringEnd = text.IndexOf('\'', stringStart + 1);
                if (stringEnd == -1) break;

                editorTextBox.SelectionStart = stringStart;
                editorTextBox.SelectionLength = stringEnd - stringStart + 1;
                editorTextBox.SelectionColor = Color.Brown;
                editorTextBox.SelectionFont = new Font(editorTextBox.Font, FontStyle.Regular);

                startPos = stringEnd + 1;
            }
        }

        private void AnalyzePascalCode(string sourceCode)
        {
            var errors = new List<CompilerError>();

            try
            {
                // Очищаем DataGridView
                dataGridViewTokens.Rows.Clear();

                var lexer = new Lexer(sourceCode);
                var tokens = lexer.GetAllTokens();

                int unknownCount = 0;
                int validTokensCount = 0;

                // Добавляем строку с информацией о начале анализа
                dataGridViewTokens.Rows.Add(0, "INFO", "=== РЕЗУЛЬТАТ ЛЕКСИЧЕСКОГО АНАЛИЗА ===", 0, 0, "");

                foreach (var token in tokens)
                {
                    if (token.Type == TokenType.Whitespace || token.Type == TokenType.NewLine)
                        continue;

                    validTokensCount++;

                    string errorMessage = "";

                    if (token.Type == TokenType.Unknown)
                    {
                        errorMessage = $"Недопустимый символ '{token.Value}'";
                        errors.Add(new CompilerError
                        {
                            Line = token.Line,
                            Column = token.Column,
                            Message = errorMessage
                        });
                        unknownCount++;
                    }

                    dataGridViewTokens.Rows.Add(
                        GetTokenCode(token.Type),
                        token.Type.ToString(),
                        token.Value,
                        token.Line,
                        token.Column,
                        errorMessage
                    );
                }

                // Добавляем пустую строку для разделения
                dataGridViewTokens.Rows.Add(0, "", "", 0, 0, "");

                // Добавляем статистику
                dataGridViewTokens.Rows.Add(0, "STATS", $"Всего токенов: {validTokensCount}", 0, 0, "");

                if (unknownCount == 0)
                {
                    dataGridViewTokens.Rows.Add(0, "SUCCESS", "ОШИБОК НЕ НАЙДЕНО", 0, 0, "");
                    dataGridViewTokens.Rows[dataGridViewTokens.Rows.Count - 1].DefaultCellStyle.BackColor = Color.LightGreen;
                    dataGridViewTokens.Rows[dataGridViewTokens.Rows.Count - 1].DefaultCellStyle.ForeColor = Color.DarkGreen;
                    dataGridViewTokens.Rows[dataGridViewTokens.Rows.Count - 1].DefaultCellStyle.Font = new Font(dataGridViewTokens.Font, FontStyle.Bold);
                }
                else
                {
                    dataGridViewTokens.Rows.Add(0, "ERROR", $"НАЙДЕНО ОШИБОК: {unknownCount}", 0, 0, "");
                    dataGridViewTokens.Rows[dataGridViewTokens.Rows.Count - 1].DefaultCellStyle.BackColor = Color.LightCoral;
                    dataGridViewTokens.Rows[dataGridViewTokens.Rows.Count - 1].DefaultCellStyle.ForeColor = Color.DarkRed;
                    dataGridViewTokens.Rows[dataGridViewTokens.Rows.Count - 1].DefaultCellStyle.Font = new Font(dataGridViewTokens.Font, FontStyle.Bold);
                }

                // Проверка на record
                if (sourceCode.ToLower().Contains("record"))
                {
                    dataGridViewTokens.Rows.Add(0, "INFO", "Обнаружено объявление record", 0, 0, "");

                    // Найдем строки с record
                    string[] lines = sourceCode.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].ToLower().Contains("record"))
                        {
                            dataGridViewTokens.Rows.Add(0, "RECORD", $"  Строка {i + 1}: {lines[i].Trim()}", 0, 0, "");
                        }
                    }

                    // Проверяем структуру record
                    CheckRecordStructure(sourceCode, errors);
                }

                AutoResizeDataGridView();
            }
            catch (Exception ex)
            {
                errors.Add(new CompilerError
                {
                    Line = 1,
                    Column = 1,
                    Message = $"Ошибка: {ex.Message}"
                });

                dataGridViewTokens.Rows.Add(0, "ERROR", $"Ошибка при анализе: {ex.Message}", 0, 0, ex.Message);
                AutoResizeDataGridView();
            }


        }

        private void CheckRecordStructure(string sourceCode, List<CompilerError> errors)
        {
            string[] lines = sourceCode.Split('\n');
            bool inRecord = false;
            int recordStartLine = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim().ToLower();

                if (line.Contains("record") && !inRecord)
                {
                    inRecord = true;
                    recordStartLine = i + 1;
                    dataGridViewTokens.Rows.Add(0, "RECORD", $"  Запись начинается в строке {recordStartLine}", 0, 0, "");
                }
                else if (inRecord && line.Contains("end"))
                {
                    inRecord = false;
                    dataGridViewTokens.Rows.Add(0, "RECORD", $"  Запись завершается в строке {i + 1}", 0, 0, "");
                }
                else if (inRecord && line.Contains(":"))
                {
                    // Показываем поля записи
                    dataGridViewTokens.Rows.Add(0, "FIELD", $"    Поле: {line}", i + 1, 1, "");
                }
            }

            if (inRecord)
            {
                string errorMsg = "Незакрытое объявление record (отсутствует 'end')";
                errors.Add(new CompilerError
                {
                    Line = recordStartLine,
                    Column = 1,
                    Message = errorMsg
                });
                dataGridViewTokens.Rows.Add(0, "ERROR", $"  {errorMsg}", recordStartLine, 1, errorMsg);
            }
        }

        private int GetTokenCode(TokenType type)
        {
            switch (type)
            {
                case TokenType.Keyword: return 1;
                case TokenType.Identifier: return 2;
                case TokenType.Number: return 3;
                case TokenType.String: return 4;
                case TokenType.Operator: return 5;
                case TokenType.Semicolon: return 6;
                case TokenType.Colon: return 7;
                case TokenType.Assign: return 8;
                case TokenType.Dot: return 9;
                case TokenType.Comma: return 10;
                case TokenType.OpenParen: return 11;
                case TokenType.CloseParen: return 12;
                case TokenType.OpenBrace: return 13;
                case TokenType.CloseBrace: return 14;
                case TokenType.OpenBracket: return 15;
                case TokenType.CloseBracket: return 16;
                case TokenType.Comment: return 17;
                case TokenType.Unknown: return 99;
                default: return 0;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!CheckUnsavedChanges())
            {
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        private void splitContainer1_Panel2_Paint(object sender, PaintEventArgs e) { }
        private void splitContainer1_Panel1_Paint(object sender, PaintEventArgs e) { }
        private void editorTextBox_TextChanged_1(object sender, EventArgs e) { }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            CreateNewFile();
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            OpenFile();
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            SaveFile();
        }

        private void pictureBox4_Click(object sender, EventArgs e)
        {
            Undo();
        }

        private void pictureBox5_Click(object sender, EventArgs e)
        {
            Redo();
        }

        private void pictureBox6_Click(object sender, EventArgs e)
        {
            editorTextBox.Copy();
        }

        private void pictureBox7_Click(object sender, EventArgs e)
        {
            editorTextBox.Cut();
        }
    }
}