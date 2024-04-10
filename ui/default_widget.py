import PyQt5.QtWidgets as qtw
from ui.ui_tools import dict_to_css, add_target
from copy import copy


class DefaultWidget(qtw.QFrame):

    def __init__(self, layout='h', border="grey_round", padding=0.7):

        super().__init__()
        match layout:
            case 'h':
                self.main_layout = qtw.QHBoxLayout(self)
            case 'v':
                self.main_layout = qtw.QVBoxLayout(self)

        self.main_layout.setContentsMargins(0, 0, 0, 0)
        self.ID = id(self)
        self.setObjectName(str(self.ID))

        # styling
        self.style_sheet = {
            "padding": f"{padding}em",
        }

        # modifying stylesheet
        match border:
            case "grey_round":
                self.style_sheet["border"] = "0.1em solid"
                self.style_sheet["border-color"] = "rgba(0, 0, 0, 0.1)"
                self.style_sheet["border-radius"] = "0.75em"
                self.style_sheet["padding"] = f"{padding}em"
                self.style_sheet["background-color"] = "rgba(0, 0, 0, 0.05)"

            case None:
                self.style_sheet["border"] = "none"

        ss_target = f"DefaultWidget#{self.ID}"

        self.css_style_sheet = dict_to_css(self.style_sheet) if isinstance(self.style_sheet, dict) else self.style_sheet
        self.css_style_sheet = add_target(self.css_style_sheet, ss_target)
        self.setStyleSheet(self.css_style_sheet)

    @staticmethod
    def cleanup() -> None:
        pass


class DefaultInputWidget(DefaultWidget):

    def __init__(self, layout='h', border="grey_round", padding=0.7) -> None:
        super().__init__(layout=layout, border=border, padding=padding)

    def get_input(self) -> None:
        pass


