import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
from ui.ui_tools import dict_to_css, add_target
from event_bus import EventBus as eb
from functools import partial
from ui.default_widget import DefaultWidget


class CustomHeader(DefaultWidget):

    def __init__(self, label, font_size=16, border=None):

        super().__init__(layout="h", border=border)

        # main layout assembling

        self.title = qtw.QLabel(label, self)
        self.title.setStyleSheet(f"font-size: {font_size}pt")
        self.main_layout.addWidget(self.title, alignment=qtc.Qt.AlignCenter)


class CustomFooter(DefaultWidget):
    
    def __init__(self, labels, border=None):
        
        super().__init__(layout="h", border=border)
        
        # main layout assembling

        self.buttons = []
        for i, label in enumerate(labels):

            if isinstance(label, tuple):
                event = label[1]
                additional = label[2:]
                label = label[0]
            else:
                label_ = label.replace(" ", "_")
                event = f"{label_}_pressed"
                additional = ()

            self.button = qtw.QPushButton(label, self)
            self.buttons.append(self.button)
            self.buttons[i].setSizePolicy(qtw.QSizePolicy.Expanding, qtw.QSizePolicy.Minimum)

            func = partial(self.emit_event, event=event, additional=additional)

            self.buttons[i].clicked.connect(func)
            self.main_layout.addWidget(self.buttons[i])

    def emit_event(self, event, additional):
        if additional == (): eb.emit(event)
        else: eb.emit(event, *additional)


class LargeButtons(DefaultWidget):

    def __init__(self, button1=(None, {}), button2=(None, {}), labels=(None, None), layout="v", border=None):

        super().__init__(layout=layout, border=border)

        self.sub_layout_1 = qtw.QHBoxLayout(self)

        # sub layout assembling
        ## sublayout for mode buttons

        self.t_mode_button = qtw.QPushButton(labels[0], self)
        self.t_mode_button.setSizePolicy(qtw.QSizePolicy.Expanding, qtw.QSizePolicy.Expanding)
        self.t_mode_button.clicked.connect(lambda: eb.emit(*button1))
        self.sub_layout_1.addWidget(self.t_mode_button)

        self.c_mode_button = qtw.QPushButton(labels[1], self)
        self.c_mode_button.setSizePolicy(qtw.QSizePolicy.Expanding, qtw.QSizePolicy.Expanding)
        self.c_mode_button.clicked.connect(lambda: eb.emit(*button2))
        self.sub_layout_1.addWidget(self.c_mode_button)

        # main layout assembling

        self.main_layout.addLayout(self.sub_layout_1)


class FileSelector(DefaultWidget):

    def __init__(self, file_selected=(None, {}), labels=(None, None), directory=False, border=None):

        super().__init__(layout="v", border=border)

        default_labels = (None, None)
        if isinstance(labels, str):
            labels = (labels,) + default_labels[1:]
        else:
            self.labels = labels + default_labels[len(labels)-1:]

        self.file_path = ""
        self.file_selected = file_selected

        self.sub_layout_1 = qtw.QHBoxLayout(self)

        # sub layout assembling

        self.sub_layout_1.addStretch(1)

        self.label_fselect = qtw.QLabel(labels[0], self)
        self.sub_layout_1.addWidget(self.label_fselect)  # 1

        self.open_file_button = qtw.QPushButton("Open Directory" if directory else "Open File", self)
        self.open_file_button.clicked.connect(self.open_directory if directory else self.open_file)
        self.sub_layout_1.addWidget(self.open_file_button)  # 2

        # main layout assembling

        self.file_path_line_edit = qtw.QLineEdit(self)
        self.file_path_line_edit.setPlaceholderText(labels[1])
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
                self.file_path,
                self.file_selected[1]
            )

    def open_directory(self):

        options = qtw.QFileDialog.Options()
        selected_dir = qtw.QFileDialog.getExistingDirectory(
            self,
            " Open Directory ",
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


class TitledLineEdit(DefaultWidget):

    def __init__(self, line_edited=(None, {}), labels=(None, None), layout="h", border=None):

        super().__init__(layout=layout, border=border)
        self.text_edit = ""
        self.line_edited = line_edited
        default_labels = (None, None)
        default_line_edited = (None, {})
        if isinstance(labels, str):
            self.labels = (labels,) + default_labels[1:]
            self.line_edited = (line_edited,) + default_line_edited[1:]
        else: self.labels = labels

        # main layout assembling

        self.label_widget = qtw.QLabel(self.labels[0], self)
        self.main_layout.addWidget(self.label_widget)

        self.l_edit = qtw.QLineEdit(self)
        self.l_edit.editingFinished.connect(self.on_line_edited)
        self.l_edit.setPlaceholderText(self.labels[1])
        self.main_layout.addWidget(self.l_edit)

    def on_line_edited(self):
        self.text_edit = self.l_edit.text()
        eb.emit(
            self.line_edited[0],
            self.l_edit.text(),
            self.line_edited[1]
        )


class TitledDropdown(DefaultWidget):

    def __init__(self, option_changed=(None, {}), labels=None, options=None, layout="h", border=None):

        super().__init__(layout=layout, border=border)
        self.option_selected = ""  # attribute holding the current selected option
        self.option_changed = option_changed  # signal attributes upon changing an option
        self.options = options  # tuple with the options

        default_labels = (None, None)
        default_option_changed = (None, {})
        if isinstance(labels, str):
            self.labels = (labels,) + default_labels[1:]
        if isinstance(option_changed, str):
            self.option_changed = (option_changed,) + default_option_changed[1:]

        # main layout assembling

        self.main_layout.addWidget(qtw.QLabel(self.labels[0]))

        self.dropdown = qtw.QComboBox(self)
        for i, item in enumerate(options):
            self.dropdown.addItem(item)
        self.dropdown.currentIndexChanged.connect(self.on_dropdown_selected)
        self.main_layout.addWidget(self.dropdown)

        self.main_layout.addStretch(1)

    def on_dropdown_selected(self):
        self.option_selected = self.dropdown.currentText()
        if isinstance(self.option_changed, tuple):
            eb.emit(self.option_changed[0], self.option_selected, self.option_changed[1])
        else:
            for tup in self.option_changed:
                eb.emit(self.tup[0], self.option_selected, self.tup[1])
