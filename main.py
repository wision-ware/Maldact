import sys
import PyQt5.QtWidgets as qtw
from ui.main_window_elements import Ui_FileSelector


class MainWindow(qtw.QMainWindow):

    def __init__(self):

        super().__init__()
        self.setWindowTitle("Maldact")

        # create and set up the UI
        self.ui = Ui_FileSelector()
        self.setCentralWidget(self.ui)

def main():

    app = qtw.QApplication(sys.argv)
    window = MainWindow()
    window.show()
    sys.exit(app.exec_())

if __name__ == "__main__":
    main()