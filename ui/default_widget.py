import PyQt5.QtWidgets as qtw
from ui.ui_tools import dict_to_css, add_target


class DefaultWidget(qtw.QFrame):

    def __init__(self, layout='h', border="grey_round", padding=0.5):

        super().__init__()
        match layout:
            case 'h':
                self.main_layout = qtw.QHBoxLayout(self)
            case 'v':
                self.main_layout = qtw.QVBoxLayout(self)

        self.main_layout.setContentsMargins(0, 0, 0, 0)

        # styling
        self.style_sheet = {
            "padding": f"{padding}em"
        }

        # modifying stylesheet
        match border:
            case "grey_round":
                self.style_sheet["border"] = "0.1em solid grey"
                self.style_sheet["border-radius"] = "0.75em"
                self.style_sheet["padding"] = f"{padding-0.1}em"
            case _:
                pass

        self.css_style_sheet = dict_to_css(self.style_sheet)
        self.css_style_sheet = add_target(self.css_style_sheet, "DefaultWidget")
        self.setStyleSheet(self.css_style_sheet)

    @staticmethod
    def cleanup():
        pass