import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from ui.widgets import UiMModeButtons, UiFileSelector

class Menu(qtw.QWidget):

    def __init__(self):

        super().__init__()

        main_layout = qtw.QVBoxLayout(self)
        self.key = 'menu'
        self.subscribe_list = ["switch_modes"]

        # sublayout assembling

        # ----

        # main layout assembling

        main_layout.addWidget(UiMModeButtons(
            button1 = (
                "Train a classification model",
                self.subscribe_list[0],
                {"ui_cls":Training}
            ),
            button2 = (
                "Classify a dataset",
                self.subscribe_list[0],
                {"ui_cls":Sorting}
            ),
            label = "Select mode"
        ))


class Training(qtw.QWidget):

    def __init__(self):

        super().__init__()

        main_layout = qtw.QVBoxLayout(self)
        self.key = 'train'

        # sublayout assembling

        # ----

        # main layout assembling

        self.title = qtw.QLabel("Model training mode")
        self.title.setStyleSheet("font-size: 16px")
        main_layout.addWidget(self.title, alignment=qtc.Qt.AlignCenter)

        #TODO model name line edit

        main_layout.addWidget(UiFileSelector(
            file_selected = "tr_samples_selected",
            label = "Select training data directory",
            directory = True
        ))

        main_layout.addWidget(UiFileSelector(
            file_selected = "parameter_dir_selected",
            label = "Select a directory for your model",
            directory = True
        ))

        main_layout.addStretch(1)


class Sorting(qtw.QWidget):

    def __init__(self):

        super().__init__()

        main_layout = qtw.QVBoxLayout(self)
        self.key = 'sort'

        # sublayout assembling

        # ----

        # main layout assembling

        self.title = qtw.QLabel("Data classification mode")
        self.title.setStyleSheet("font-size: 16px")
        main_layout.addWidget(self.title, alignment=qtc.Qt.AlignCenter)

        main_layout.addWidget(UiFileSelector(
            file_selected = "sample_dir_selected",
            label = "Select directory with samples for classification",
            directory = True
        ))

        main_layout.addWidget(UiFileSelector(
            file_selected="model_selected",
            label="Select your trained model"
        ))

        main_layout.addStretch(1)