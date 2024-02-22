import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
from ui.ui_tools import dict_to_css, add_target
from event_bus import EventBus as eb
from functools import partial
from ui.default_widget import DefaultWidget, DefaultInputWidget


class CustomHeader(DefaultInputWidget):

    def __init__(self, label, font_size=16, border=None):

        super().__init__(layout="h", border=border)

        # main layout assembling

        self.title = qtw.QLabel(label, self)
        self.title.setStyleSheet(f"font-size: {font_size}pt")
        self.main_layout.addWidget(self.title, alignment=qtc.Qt.AlignCenter)


class CustomFooter(DefaultInputWidget):
    
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


class LargeButtons(DefaultInputWidget):

    def __init__(self, button1=None, button2=None, labels=None, layout="v", border=None):

        super().__init__(layout=layout, border=border)

        self.button1_output = button1
        self.button2_output = button2
        self.labels = labels

        if not isinstance(button1, tuple):
            self.button1 = button1, {}
        if not isinstance(button2, tuple):
            self.button2 = button2, {}
        if not isinstance(labels, tuple):
            self.labels = labels, None

        self.sub_layout_1 = qtw.QHBoxLayout(self)

        # sub layout assembling
        # sub layout for mode buttons

        self.button1 = qtw.QPushButton(self.labels[0], self)
        self.button1.setSizePolicy(qtw.QSizePolicy.Expanding, qtw.QSizePolicy.Expanding)
        self.button1.clicked.connect(lambda: eb.emit(*self.button1_output))
        self.sub_layout_1.addWidget(self.button1)

        self.button2 = qtw.QPushButton(self.labels[1], self)
        self.button2.setSizePolicy(qtw.QSizePolicy.Expanding, qtw.QSizePolicy.Expanding)
        self.button2.clicked.connect(lambda: eb.emit(*self.button2_output))
        self.sub_layout_1.addWidget(self.button2)

        # main layout assembling

        self.main_layout.addLayout(self.sub_layout_1)


class FileSelector(DefaultInputWidget):

    def __init__(self, file_selected=None, labels=None, directory=False, border=None) -> None:

        super().__init__(layout="v", border=border)

        self.file_path = ""
        self.file_selected = file_selected

        if not isinstance(labels, tuple):
            self.labels = labels, None
        if not isinstance(file_selected, tuple):
            self.file_selected = file_selected, {}

        self.sub_layout_1 = qtw.QHBoxLayout(self)

        # sub layout assembling

        self.sub_layout_1.addStretch(1)

        self.label_fselect = qtw.QLabel(self.labels[0], self)
        self.sub_layout_1.addWidget(self.label_fselect)  # 1

        self.open_file_button = qtw.QPushButton("Open Directory" if directory else "Open File", self)
        self.open_file_button.clicked.connect(self.open_directory if directory else self.open_file)
        self.sub_layout_1.addWidget(self.open_file_button)  # 2

        # main layout assembling

        self.file_path_line_edit = qtw.QLineEdit(self)
        self.file_path_line_edit.setPlaceholderText(self.labels[1])
        self.main_layout.addWidget(self.file_path_line_edit)  # 1

        self.main_layout.addLayout(self.sub_layout_1)  # 2

    def open_file(self) -> None:

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
            if isinstance(self.file_selected[0], str):
                eb.emit(
                    self.file_selected[0],
                    self.file_path,
                    self.file_selected[1]
                )

    def get_input(self) -> str:

        return self.file_path

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


class TitledLineEdit(DefaultInputWidget):

    def __init__(self, line_edited=None, labels=None, layout="h", border=None):

        super().__init__(layout=layout, border=border)
        self.text_edit = ""
        self.line_edited = line_edited
        self.labels = labels
        if not isinstance(labels, tuple):
            self.labels = labels, None
        if not isinstance(line_edited, tuple):
            self.line_edited = line_edited, {}

        # main layout assembling

        self.label_widget = qtw.QLabel(self.labels[0], self)
        self.main_layout.addWidget(self.label_widget)

        self.l_edit = qtw.QLineEdit(self)
        self.l_edit.editingFinished.connect(self.on_line_edited)
        self.l_edit.setPlaceholderText(self.labels[1])
        self.main_layout.addWidget(self.l_edit)

    def on_line_edited(self):
        self.text_edit = self.l_edit.text()
        if isinstance(self.line_edited[0], str):
            eb.emit(
                self.line_edited[0],
                self.l_edit.text(),
                self.line_edited[1]
            )

    def get_input(self) -> str:

        return self.text_edit


class TitledDropdown(DefaultInputWidget):

    def __init__(self, option_changed=None, labels=None, options=None, layout="h", border=None):

        super().__init__(layout=layout, border=border)
        self.option_selected = ""  # attribute holding the current selected option
        self.option_changed = option_changed  # signal attributes upon changing an option
        self.options = options  # tuple with the options

        if not isinstance(labels, tuple):
            self.labels = labels, None
        if not isinstance(option_changed, tuple):
            self.option_changed = option_changed, {}

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
        if isinstance(self.option_changed[0], str):
            eb.emit(self.option_changed[0], self.option_selected, self.option_changed[1])

    def get_input(self) -> str:

        return self.option_selected
