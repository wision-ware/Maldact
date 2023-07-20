import PyQt5.QtWidgets as qtw

class Ui_FileSelector(qtw.QWidget):

    def __init__(self):

        super().__init__()

        # main layout
        main_layout = qtw.QVBoxLayout(self)

        # sublayouts
        sub_layout_1 = qtw.QHBoxLayout(self)

        # sublayout assembling

        ## sublayout for file select label
        sub_layout_1.addStretch(1)

        self.label_fselect = qtw.QLabel("Select training data file")
        sub_layout_1.addWidget(self.label_fselect)  # 1

        self.open_file_button = qtw.QPushButton("Open File", self)
        self.open_file_button.clicked.connect(self.open_file)
        sub_layout_1.addWidget(self.open_file_button) # 2

        # main layout assembling

        self.file_path_line_edit = qtw.QLineEdit(self)
        main_layout.addWidget(self.file_path_line_edit)  # 1

        main_layout.addLayout(sub_layout_1)  # 2

        main_layout.addStretch(1)

    def open_file(self):

        options = qtw.QFileDialog.Options()
        file_path, _ = qtw.QFileDialog.getOpenFileName(
            self,
            "Open File",
            "",
            "All Files (*);;Text Files (*.txt)",
            options=options
        )

        if file_path:
            self.file_path_line_edit.setText(file_path)