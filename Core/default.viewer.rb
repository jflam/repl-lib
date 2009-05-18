
# Default visualizer

class Object
  def as_xaml
    "=> #{self}"
  end
end

class NilClass
  def as_xaml
    "=> nil"
  end
end

class Array
  def as_xaml
    "=> #{self.inspect}"
  end
end
