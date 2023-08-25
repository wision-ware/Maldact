import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from event_bus import EventBus as eb
from functools import partial


class CustomHeader(qtw.QWidget):

    def __init__(self, label, font_size=16):

        super().__init__()

        self.main_layout = qtw.QHBoxLayout(self)

        # main layout assembling

        self.title = qtw.QLabel(label, self)
        self.title.setStyleSheet(f"font-size: {font_size}px")
        self.main_layout.addWidget(self.title, alignment=qtc.Qt.AlignCenter)


class CustomFooter(qtw.QWidget):
    
    def __init__(self, labels):
        
        super().__init__()
        
        self.main_layout = qtw.QHBoxLayout(self)
        
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



class LargeButtons(qtw.QWidget):

    def __init__(self, button1=(None, {}), button2=(None, {}), labels=(None, None)):

        super().__init__()

        self.main_layout = qtw.QVBoxLayout(self)
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


class FileSelector(qtw.QWidget):

    def __init__(self, file_selected=(None, {}), labels=(None, None), directory=False):

        super().__init__()

        default_labels = (None, None)
        if isinstance(labels, str):
            labels = (labels,) + default_labels[1:]
        else:
            self.labels = labels + default_labels[len(labels)-1:]

        self.file_path = ""
        self.file_selected = file_selected

        self.main_layout = qtw.QVBoxLayout(self)

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

    def __init__(self, line_edited=(None, {}), labels=(None, None), layout="h"):

        super().__init__()
        self.text_edit = ""
        self.line_edited = line_edited

        default_labels = (None, None)
        default_line_edited = (None, {})
        if isinstance(labels, str):
            self.labels = (labels,) + default_labels[1:]
            self.line_edited = (line_edited,) + default_line_edited[1:]
        else: self.labels = labels

        match layout:
            case "v":
                self.main_layout = qtw.QVBoxLayout(self)
            case "h":
                self.main_layout = qtw.QHBoxLayout(self)

        # main layout assembling

        self.label_widget = qtw.QLabel(self.labels[0], self)
        self.main_layout.addWidget(self.label_widget)

        self.l_edit = qtw.QLineEdit(self)
        self.l_edit.editingFinished.connect(self.on_line_edited)
        self.l_edit.setPlaceholderText(self.labels[1])
        self.main_layout.addWidget(self.l_edit)
        self.main_layout.addStretch(1)

    def on_line_edited(self):
        self.text_edit = self.l_edit.text()
        eb.emit(
            self.line_edited[0],
            self.l_edit.text(),
            self.line_edited[1]
        )


class TitledDropdown(qtw.QWidget):

    def __init__(self, option_changed=(None, {}), labels=None, options=None, layout="h"):

        super().__init__()
        self.option_selected = ""  # attribute holding the current selected option
        self.option_changed = option_changed  # signal attributes upon changing an option
        self.options = options  # tuple with the options

        # style configuration
        self.style_sheet = ""
        self.setStyleSheet(self.style_sheet)

        default_labels = (None, None)
        default_option_changed = (None, {})
        if isinstance(labels, str):
            self.labels = (labels,) + default_labels[1:]
        if isinstance(option_changed, str):
            self.option_changed = (option_changed,) + default_option_changed[1:]

        match layout:
            case "v":
                self.main_layout = qtw.QVBoxLayout(self)
            case "h":
                self.main_layout = qtw.QHBoxLayout(self)

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
        eb.emit(self.option_changed[0], self.option_selected, self.option_changed[1])


class DefaultWidget(qtw.QWidget):

    def __init__(self, layout='h', border="grey_round"):

        super().__init__()
        match layout:
            case 'h':
                self.main_layout = qtw.QHBoxLayout(self)
                self.main_layout.setContentsMargins(0, 0, 0, 0)
            case 'v':
                self.main_layout = qtw.QVBoxLayout(self)
                self.main_layout.setContentsMargins(0, 0, 0, 0)

        # styling
        self.style_sheet = {}

        # modifying stylesheet
        match border:
            case "grey_round":
                self.style_sheet["border"] = "2px solid grey"
                self.style_sheet["border-radius"] = "10px"

        # converting to css
        self.css_style_sheet = DefaultWidget.dict_to_css(self.style_sheet)

        self.setStyleSheet(self.style_sheet)

    @staticmethod
    def dict_to_css(dict_styles):
        css = ""
        for selector, styles in dict_styles.items():
            css += f"{selector} {{\n"
            for prop, value in styles.items():
                css += f"    {prop}: {value};\n"
            css += "}\n"
        return css