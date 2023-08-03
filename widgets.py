import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from signal_manager import SignalManager as sm


class UiMModeButtons(qtw.QWidget):

    def __init__(self, mw_instance):

        super().__init__()

        main_layout = qtw.QVBoxLayout(self)

        sub_layout_1 = qtw.QHBoxLayout(self)

        # sublayout assembling
        ## sublayout for mode buttons

        self.t_mode_button = qtw.QPushButton("Train a classification model", self)
        self.t_mode_button.setSizePolicy(qtw.QSizePolicy.Expanding, qtw.QSizePolicy.Expanding)
        sub_layout_1.addWidget(self.t_mode_button)

        self.c_mode_button = qtw.QPushButton("Classify a dataset", self)
        self.c_mode_button.setSizePolicy(qtw.QSizePolicy.Expanding, qtw.QSizePolicy.Expanding)
        sub_layout_1.addWidget(self.c_mode_button)

        # main layout assembling

        self.label_mselect = qtw.QLabel("Select mode")
        self.label_mselect.setStyleSheet("font-size: 16px")
        main_layout.addWidget(self.label_mselect, alignment=qtc.Qt.AlignCenter)

        main_layout.addLayout(sub_layout_1)


class UiFileSelector(qtw.QWidget):

    def __init__(self):

        super().__init__()

        main_layout = qtw.QVBoxLayout(self)

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