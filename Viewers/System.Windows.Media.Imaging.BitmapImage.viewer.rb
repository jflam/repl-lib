# System.Windows.Media.Imaging.BitmapImage viewer

# TODO: eliminate most of the boilerplate here. The tricky part is mapping from the fully qualified type name
# to the assembly that we need to require at the top of the file. Once that's done, we should be able to 
# module_eval just the as_xaml method in this file.

require 'PresentationCore'
require 'PresentationFramework'

include System::Windows::Controls
include System::Windows::Documents

module System
  module Windows
    module Media
      module Imaging
        class BitmapImage
          def as_xaml
            image = Image.new
            image.width = 400
            image.source = self
            
            b = Border.new
            b.child = image
            b
          end
        end
      end
    end
  end
end
