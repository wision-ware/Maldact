import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from event_bus import EventBus as eb


class UiMModeButtons(qtw.QWidget):

    def __init__(self, button1=(None,None,{}), button2=(None,None,{}), label=None):

        super().__init__()

        self.main_layout = qtw.QVBoxLayout(self)
        self.sub_layout_1 = qtw.QHBoxLayout(self)

        # sub layout assembling
        ## sublayout for mode buttons

        self.t_mode_button = qtw.QPushButton(button1[0], self)
        self.t_mode_button.setSizePolicy(qtw.QSizePolicy.Expanding, qtw.QSizePolicy.Expanding)
        self.t_mode_button.clicked.connect(lambda: eb.emit(button1[1], button1[2]))
        self.sub_layout_1.addWidget(self.t_mode_button)

        self.c_mode_button = qtw.QPushButton(button2[0], self)
        self.c_mode_button.setSizePolicy(qtw.QSizePolicy.Expanding, qtw.QSizePolicy.Expanding)
        self.c_mode_button.clicked.connect(lambda: eb.emit(button2[1], button2[2]))
        self.sub_layout_1.addWidget(self.c_mode_button)

        # main layout assembling

        self.label_mselect = qtw.QLabel(label)
        self.label_mselect.setStyleSheet("font-size: 16px")
        self.main_layout.addWidget(self.label_mselect, alignment=qtc.Qt.AlignCenter)

        self.main_layout.addLayout(self.sub_layout_1)


class UiFileSelector(qtw.QWidget):

    def __init__(self, file_selected=(None,{}), label=None, directory=False):

        super().__init__()
        self.file_path = ""
        self.file_selected = file_selected

        self.main_layout = qtw.QVBoxLayout(self)

        self.sub_layout_1 = qtw.QHBoxLayout(self)

        # sub layout assembling

        self.sub_layout_1.addStretch(1)

        self.label_fselect = qtw.QLabel(label)
        self.sub_layout_1.addWidget(self.label_fselect)  # 1

        self.open_file_button = qtw.QPushButton("Open Directory" if directory else "Open File", self)
        self.open_file_button.clicked.connect(self.open_directory if directory else self.open_file)
        self.sub_layout_1.addWidget(self.open_file_button)  # 2

        # main layout assembling

        self.file_path_line_edit = qtw.QLineEdit(self)
        self.main_layout.addWidget(self.file_path_line_edit)  # 1

        self.main_layout.addLayout(self.sub_layout_1)  # 2

    def open_file(self):

        options = qtw.QFileDialog.Options()
        file_path, _ = qtw.QFileDialog.getOpenFileName(
            self,
            "Open File",
            "",
            "All Files (*);;Text Files (*.txt)",
            options=options
        )
        if self.file_path != file_path:
            self.file_path_line_edit.setText(file_path)
            self.file_path = file_path
            eb.emit(
                self.file_selected[0],
                file_path,
                self.file_selected[1]
            )

    def open_directory(self):

        options = qtw.QFileDialog.Options()
        selected_dir = qtw.QFileDialog.getExistingDirectory(
            self,
            "Open Directory",
            options=options
        )
        if self.file_path != selected_dir:
            self.file_path_line_edit.setText(selected_dir)
            self.file_path = selected_dir
            eb.emit(
                self.file_selected[0],
                selected_dir,
                self.file_selected[1]
            )


class TitledLineEdit(qtw.QWidget):

    def __init__(self, line_edited=(None,{}), label=None):

        super().__init__()
        self.text_edit = ""
        self.line_edited = line_edited

        self.main_layout = qtw.QVBoxLayout()

        # main layout assembling

class DefaultWidget(qtw.QWidget):
    def __init__(self): super().__init__()
