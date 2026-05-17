using System;
using System.Collections.Generic;
using EffectViewer.Localization;
using EffectViewer.Projects;

namespace EffectViewer.ViewModels
{
    public sealed class HelpSectionViewModel
    {
        public HelpSectionViewModel(string title, string summary, string body)
        {
            Title = title ?? string.Empty;
            Summary = summary ?? string.Empty;
            Body = body ?? string.Empty;
        }

        public string Title { get; }
        public string Summary { get; }
        public string Body { get; }
    }

    public sealed class HelpEditorViewModel : EditorViewModelBase
    {
        public const string DocumentKey = "effectviewer_help";

        private IReadOnlyList<HelpSectionViewModel> _sections;
        private HelpSectionViewModel _selectedSection;

        public IReadOnlyList<HelpSectionViewModel> Sections
        {
            get => _sections;
            private set => SetProperty(ref _sections, value ?? []);
        }

        public string Subtitle => LocalizationManager.Instance.Text("Help.Subtitle");

        public HelpSectionViewModel SelectedSection
        {
            get => _selectedSection;
            set => SetProperty(ref _selectedSection, value);
        }

        public HelpEditorViewModel()
            : base(LocalizationManager.Instance.Text("Help.Title"), EffectAssetKind.Help)
        {
            DocumentId = CreateDocumentId(EffectAssetKind.Help, DocumentKey);
            Sections = CreateSectionsForCurrentLanguage();
            SelectedSection = Sections.Count > 0 ? Sections[0] : null;
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }

        public override void Dispose()
        {
            LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
            base.Dispose();
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            Title = LocalizationManager.Instance.Text("Help.Title");
            Sections = CreateSectionsForCurrentLanguage();
            SelectedSection = Sections.Count > 0 ? Sections[0] : null;
            OnPropertyChanged(nameof(Subtitle));
        }

        private static IReadOnlyList<HelpSectionViewModel> CreateSectionsForCurrentLanguage()
        {
            return LocalizationManager.Instance.IsChinese
                ? CreateChineseSections()
                : CreateEnglishSections();
        }

        private static IReadOnlyList<HelpSectionViewModel> CreateChineseSections()
        {
            return
            [
                new HelpSectionViewModel(
                    "项目工作流",
                    "从创建项目、导入资源、编辑预览到保存导出的完整流程。",
                    """
                    基本概念

                    - 项目：EffectViewer 的资源容器。项目清单是 project.effectproj.json，资源通常放在 assets/images、assets/fonts、assets/reanims、assets/particles、assets/trails 和 scripts 中。
                    - 资源 ID：动画、粒子、拖尾和脚本互相引用时使用的名字。修改 ID 后，已经写在其他资源或 Lua 脚本里的旧引用不会自动批量替换。
                    - 项目路径：清单中保存的项目内相对路径，例如 assets/particles/fire.xml 或 scripts/demo.lua。
                    - 标签页星号：标题后出现 * 表示当前编辑器有未保存修改。

                    常用工作流

                    1. 新建、打开或导入项目：使用“项目 -> 新建项目...”“项目 -> 打开项目...”或“项目 -> 导入项目 Zip...”。已有当前项目后，可用“资源 -> 导入资源文件夹...”或“资源 -> 导入资源 Pak...”追加素材。
                    2. 管理资源：在项目资源管理器中展开图像、字体、动画、粒子、轨迹、展示分组；也可以用搜索框按 ID、路径或类型过滤。
                    3. 打开资源编辑：点击资源后会打开对应编辑器。图像和字体用于检查贴图/字形，动画、粒子、拖尾和 Showcase 用于编辑特效行为。
                    4. 在预览中验证：大多数编辑器都有中央预览视口。滚轮缩放，中键拖动画布，双击重置视图。
                    5. 保存修改：使用“文件 -> 保存当前文件”“文件 -> 保存全部文件”或“项目 -> 保存项目”。动画、粒子、拖尾和 Showcase 会写回资源文件；图像、字体和部分元数据写入项目清单。
                    6. 导出结果：使用“文件 -> 导出当前文件...”导出当前资源文件；使用“文件 -> 导出预览...”导出当前画面或动画；使用“项目 -> 导出项目 Zip...”打包整个项目。
                    7. 组合展示：需要把多种资源放在同一画面里时，新建或打开 Showcase，使用 Lua 创建 reanim、particle、trail，再运行脚本检查整体效果。

                    推荐习惯

                    - 先导入图像，再编辑引用这些图像的 reanim、particle、trail。
                    - 每次改完资源 ID 后，检查图像引用、脚本引用和项目资源管理器中的资源名。
                    - 做复杂动画或粒子时，经常保存当前文件；导出项目 Zip 可作为阶段性备份。
                    - 最终交付前，用“导出预览”导出 PNG 或 GIF/WebP 快速检查范围、透明度和播放节奏。
                    """),
                new HelpSectionViewModel(
                    "导入导出功能与格式",
                    "项目、资源文件、资源文件夹、Pak 和预览导出的入口与格式说明。",
                    """
                    导入项目 Zip

                    - 入口：项目 -> 导入项目 Zip...
                    - 用途：导入 EffectViewer 导出的完整项目包，或包含 project.effectproj.json 的项目 Zip。
                    - 规则：Zip 中必须包含项目清单；清单可位于根目录或某个子目录。导入后会复制到应用私有项目目录，并自动生成不冲突的项目文件夹。
                    - 安全限制：Zip 内不能包含绝对路径或 .. 路径穿越。

                    导出项目 Zip

                    - 入口：项目 -> 导出项目 Zip...
                    - 用途：打包当前内部项目，用于备份、迁移或分享。
                    - 行为：导出前会保存项目清单，并尝试保存已打开的未保存编辑器；随后压缩项目目录内文件。

                    导入单个资源文件

                    - 入口：资源 -> 导入资源文件...
                    - 用途：把一个外部文件复制到当前项目，并加入项目清单。
                    - 支持格式：
                      Image：.png、.jpg、.jpeg、.bmp、.gif、.webp、.tga
                      Font：.ttf
                      Reanim：.reanim、.reanim.compiled
                      Particle：.xml、.xml.compiled
                      Trail：.trail、.trail.compiled
                      ShowCase：.lua
                    - 导入规则：图像 ID 通常以 IMAGE_ 开头；其他资源 ID 默认来自文件名。若 ID 或路径冲突，会自动追加序号。导入后会打开对应编辑器。

                    导入资源文件夹

                    - 入口：资源 -> 导入资源文件夹...
                    - 用途：把外部素材目录追加到当前项目，并加入项目清单。
                    - 冲突处理：如果导入资源的 ID 已存在，会询问跳过、覆盖或同时保留；也可以对后续冲突执行相同操作。
                    - 推荐结构：
                      properties/resources.xml
                      reanim/*.reanim
                      particles/*.xml
                      particles/*.trail
                    - 编译资源也可导入：
                      compiled/reanim/*.reanim.compiled
                      compiled/particles/*.xml.compiled
                      compiled/particles/*.trail.compiled
                      compiled/trails/*.trail.compiled
                    - resources.xml 会用于解析 Image、Font、rows、cols、SetDefaults path 和 idprefix。
                    - Alpha 伴随图可命名为 _name.png 或 name_.png；单独存在的伴随图会作为 alphaOnly 图像处理。

                    导入资源 Pak

                    - 入口：资源 -> 导入资源 Pak...
                    - 用途：把 .pak 当作资源文件夹读取，并追加到当前项目。
                    - 行为：与导入资源文件夹类似，会按冲突选择跳过、覆盖或同时保留重复 ID。

                    拖放导入

                    - 可把单个支持资源文件或 .pak 拖到项目资源管理器。
                    - 单个资源文件和 .pak 都需要当前已有可写项目；.pak 会走 Pak 导入流程并追加到当前项目。
                    - 资源文件夹和项目 Zip 不支持拖放导入。

                    导出当前文件

                    - 入口：文件 -> 导出当前文件...
                    - 用途：把当前编辑器对应的资源文件导出到项目外。
                    - Image、Font、ShowCase：导出项目中的原始文件。
                    - Reanim：可导出 .reanim 或 .reanim.compiled。
                    - Particle：可导出 .xml 或 .xml.compiled。
                    - Trail：可导出 .trail 或 .trail.compiled。
                    - 若当前编辑器有未保存修改，导出前会先保存。

                    导出预览

                    - 入口：文件 -> 导出预览...
                    - 支持编辑器：Image、Font、Reanim、Particle、Trail、ShowCase。
                    - 格式：
                      Png：单张 PNG，适合静态检查。
                      PngSequenceZip：逐帧 PNG 序列压缩包，适合后期合成或逐帧检查。
                      Gif：兼容性好，适合快速分享。
                      Webp：压缩率较高，适合 Web 使用。
                    - 通用选项：
                      格式：选择输出类型。
                      画布倍率：控制输出分辨率倍率。
                      FPS：动画导出的帧率。
                      时长：没有明确时间线时的动画捕获总时长。
                    - Reanim 可选择完整时间线或识别出的图层范围，并设置起始帧/终止帧。
                    - Particle 和 Trail 使用起始秒/终止秒，也可选择导出到模拟结束。
                    - ShowCase 导出会重新运行脚本，再从初始状态捕获帧。
                    """),
                new HelpSectionViewModel(
                    "动画编辑功能",
                    "Reanim 编辑器中所有主要可编辑区域的作用，以及创建动画的基本流程。",
                    """
                    适用资源

                    - Reanim 编辑器用于 .reanim 和 .reanim.compiled 动画定义。
                    - 它适合编辑轨道、帧、变换、图像绑定、文本帧和补间元数据。

                    主界面

                    - 中央预览视口：显示当前动画。滚轮缩放，中键拖动，双击重置。
                    - 属性面板：编辑资源 ID、结构信息、当前帧变换、补间、图像引用和错误信息。
                    - 底部时间线：按轨道和帧显示动画结构，可选择帧、播放动画和切换轨道可见性。

                    创建动画的基本流程

                    1. 使用“资源 -> 新建资源...”创建 Reanim，输入资源 ID。
                    2. 在“动画 -> 添加轨道”中增加需要的图层，例如 body、arm、fx_fire。
                    3. 在属性面板“变换”里设置轨道名称、图像 ID 或文本内容。
                    4. 设置当前帧的 X/Y、缩放、倾斜、图集帧、透明度和可见性。
                    5. 使用“动画 -> 添加帧”扩展时间线，在不同帧上修改对象位置或图像帧。
                    6. 如需连续变化，使用“补间 -> 添加”创建补间，或先编辑关键帧后点击“检测”推断补间。
                    7. 在时间线设置 FPS 并勾选“播放”检查速度。
                    8. 检查“图像引用”中的缺失项，导入或修正对应图像 ID。
                    9. 保存当前文件；需要交付时用“导出当前文件”导出源格式或编译格式。

                    时间线控件

                    - 图层下拉框：切换完整时间线或某个识别出的动画层/轨道范围。导出预览时也会使用这些范围。
                    - FPS：每秒播放多少帧，影响编辑器预览速度和预览导出的默认帧率。
                    - 播放：开始或暂停时间线预览。
                    - 自由变换：允许在视口中拖拽当前选中帧对象。对象内部用于移动，方形控制点用于缩放，菱形控制点用于倾斜。

                    轨道和帧操作

                    - 添加轨道：在动画末尾新增轨道，新轨道帧数与当前动画一致。
                    - 移除轨道：删除当前选中轨道，至少保留一条轨道。
                    - 添加帧：在当前选中帧之后插入一帧，所有轨道都会同步增加。
                    - 移除帧：删除当前选中帧，至少保留一帧。
                    - 轨道可见性：时间线左侧可切换某条轨道是否参与预览。

                    当前帧变换字段

                    - ID：资源 ID。脚本和其他资源引用该动画时使用。
                    - 轨道名称：当前轨道名称。命名清晰后，补间、图层选择和脚本查找轨道都会更容易。
                    - 图像 ID：当前帧绑定的图像资源 ID。为空时该帧可能不绘制图像。
                    - 字体：当前帧使用的字体 ID。通常配合“文本”字段绘制文字。
                    - 文本：当前帧显示的文本内容。
                    - X / Y：当前帧的平移位置。
                    - 缩放 X / 缩放 Y：当前帧在水平和垂直方向的缩放。
                    - 倾斜 X / 倾斜 Y：当前帧的旋转/倾斜相关参数，用于形成角度变化。
                    - 帧：图集帧索引。对于多行多列图像，决定显示哪一个格子；-1 通常代表不可见。
                    - 透明度：当前帧透明度，范围 0 到 1。
                    - 可见：控制当前帧是否显示。关闭后会把帧设为不可见状态。

                    变换编辑限制

                    - 当前帧位于补间范围内时，编辑器会禁用直接变换编辑。
                    - 需要编辑补间中间帧时，可先点击“关键帧”，或烘焙/移除对应补间。

                    补间功能

                    - 检测：根据当前帧数据推断补间元数据。
                    - 添加：为当前轨道和当前帧附近创建补间。
                    - 全部烘焙：把所有补间结果写入帧数据，并清空补间元数据。
                    - 轨道：补间所在轨道序号，从 1 开始显示。
                    - 轨道名称：只读显示补间所在轨道名称。
                    - 起始帧：补间开始帧，从 1 开始显示。
                    - 终止帧：补间结束帧，从 1 开始显示。
                    - 锚点 X / 锚点 Y：补间计算使用的锚点。
                    - 关键帧：把当前补间中的选中帧拆为可直接编辑的关键帧。
                    - 烘焙：把选中补间写入帧数据并移除该补间。
                    - 移除：删除选中的补间元数据。

                    图像引用与错误

                    - 引用：动画文件中出现过的图像 ID。
                    - 已解析：项目中能找到的图像资源。
                    - 缺失：动画引用但项目不存在的图像 ID。
                    - 错误：显示文件解析、加载或编码相关问题。
                    """),
                new HelpSectionViewModel(
                    "粒子特效编辑功能",
                    "Particle 编辑器中发射器、标记、参数轨道和场字段的作用说明。",
                    """
                    适用资源

                    - Particle 编辑器用于 .xml 和 .xml.compiled 粒子系统。
                    - 一个粒子系统可包含多个发射器，每个发射器有自己的贴图、生命周期、生成速度、颜色、变换、渲染和场设置。

                    主界面

                    - 中央预览视口：实时播放当前粒子系统。
                    - 属性面板：编辑资源 ID、发射器列表、发射器字段、参数轨道、粒子场、系统场、图像引用和错误信息。

                    基本编辑流程

                    1. 创建或导入 Particle 资源。
                    2. 在“定义”区域选择一个发射器，或点击“添加”创建新发射器。
                    3. 在“常规”里设置“名称”“图像 ID”“图像行”“图像列”“图像帧数”和“发射器类型”。
                    4. 按需要打开“标记”开关，决定循环、混合、跟随等行为。
                    5. 调整“生命周期”“生成”“发射器形状”“发射”“系统颜色”“粒子颜色”“粒子变换”“渲染”和“碰撞”中的参数轨道。
                    6. 如需力场或额外运动，添加“粒子场”或“系统场”，并设置“场类型”、x、y 轨道。
                    7. 检查预览和“图像引用”里的“缺失”项。
                    8. 保存当前文件，或导出 .xml / .xml.compiled。

                    发射器列表

                    - 发射器下拉框：选择正在编辑的发射器。
                    - 添加：追加一个新发射器。
                    - 移除：删除当前发射器。至少保留一个发射器时更安全。
                    - 显示名称：有“名称”时显示“序号. 名称”，没有“名称”时显示“发射器 序号”。

                    常规字段

                    - 名称：发射器名称，便于区分多个发射器。
                    - 图像 ID：粒子使用的图像资源 ID。为空或缺失时通常看不到粒子贴图。
                    - 图像行：图集行索引，从 0 开始。
                    - 图像列：图集列索引，从 0 开始。
                    - 图像帧数：粒子动画可使用的图集帧数量，最小为 1。
                    - 发射器类型：控制发射器形状或发射方式。
                    - 动画：开启后按图像帧动画播放；播放速度由 AnimationRate 轨道控制。

                    标记开关

                    - 随机发射旋转：粒子发射时使用随机初始旋转。
                    - 对齐发射旋转：粒子旋转方向跟随发射方向。
                    - 对齐到像素：将粒子位置对齐到像素，减少亚像素抖动。
                    - 系统循环：粒子系统生命周期结束后循环。
                    - 粒子循环：单个粒子的动画或生命周期行为循环。
                    - 粒子不跟随：粒子生成后不继续跟随发射器移动。
                    - 过载时消失：粒子数量过多或超过限制时允许系统提前消失。
                    - 叠加：使用加法混合，适合火焰、发光、能量等亮色效果。
                    - 全屏：按全屏/屏幕空间方式处理相关渲染。
                    - 仅软件：只在软件渲染路径使用。
                    - 仅硬件：只在硬件渲染路径使用。

                    参数轨道通用字段

                    - 时间：节点所在时间百分比，范围 0 到 100。多个节点会按时间排序。
                    - 低值：该节点的最小值。
                    - 高值：该节点的最大值。低值和高值不同时，会在两者之间取随机或分布值。
                    - 曲线：该节点到下个节点之间的变化曲线。
                    - 分布：低值到高值之间的取值分布。
                    - 重置：恢复为默认单节点轨道。
                    - 添加：新增轨道节点。
                    - 复制：复制当前节点，并把时间略微后移。
                    - 移除：删除当前节点；每条轨道至少保留一个节点。

                    生命周期轨道

                    - SystemDuration：整个粒子系统持续时间。
                    - CrossFadeDuration：跨发射器或状态切换时的淡入淡出时间。
                    - ParticleDuration：单个粒子的寿命。

                    生成轨道

                    - SpawnRate：单位时间生成粒子的速度。
                    - SpawnMinActive：保持的最小活跃粒子数；-1 通常表示不强制。
                    - SpawnMaxActive：允许的最大活跃粒子数；过低会限制粒子密度。
                    - SpawnMaxLaunched：最多发射粒子总数；达到后会停止继续发射。

                    发射器形状轨道

                    - EmitterRadius：圆形或半径相关发射范围。
                    - EmitterOffsetX / EmitterOffsetY：发射器相对原点的偏移。
                    - EmitterBoxX / EmitterBoxY：矩形发射范围的宽高或盒形范围。
                    - EmitterSkewX / EmitterSkewY：发射区域的倾斜。
                    - EmitterPath：沿路径或路径参数相关的发射控制。

                    发射轨道

                    - LaunchSpeed：粒子初始速度。
                    - LaunchAngle：粒子初始发射角度。

                    系统颜色轨道

                    - SystemRed / SystemGreen / SystemBlue：系统级 RGB 颜色倍率。
                    - SystemAlpha：系统级透明度倍率。
                    - SystemBrightness：系统级亮度倍率。

                    粒子颜色轨道

                    - ParticleRed / ParticleGreen / ParticleBlue：单个粒子的 RGB 颜色倍率。
                    - ParticleAlpha：单个粒子的透明度。
                    - ParticleBrightness：单个粒子的亮度。

                    粒子变换轨道

                    - ParticleScale：粒子整体缩放。
                    - ParticleStretch：粒子拉伸比例。
                    - ParticleSpinAngle：粒子初始或当前旋转角度。
                    - ParticleSpinSpeed：粒子旋转速度。

                    渲染轨道

                    - AnimationRate：图像帧动画播放速度。
                    - ClipTop / ClipBottom / ClipLeft / ClipRight：从上、下、左、右裁剪粒子绘制区域。

                    碰撞轨道

                    - CollisionReflect：碰撞后的反弹强度。
                    - CollisionSpin：碰撞后附加的旋转变化。

                    粒子场和系统场

                    - 粒子场：通常作用于单个粒子，用于改变粒子的速度、位置、加速度或其他运动参数。
                    - 系统场：通常作用于系统或发射器上下文，影响整体运动或生成环境。
                    - 场类型：选择字段的作用类型。
                    - x：X 方向或第一参数轨道。
                    - y：Y 方向或第二参数轨道。
                    - 添加：新增字段。
                    - 移除：删除当前字段。

                    图像引用与错误

                    - 引用：粒子定义中用到的图像 ID。
                    - 已解析：项目中存在且能加载的图像。
                    - 缺失：粒子引用但项目中不存在的图像。
                    - 如果预览看不到粒子，优先检查图像 ID、SpawnRate、ParticleDuration、ParticleAlpha、图像行、图像列和图像帧数。
                    """),
                new HelpSectionViewModel(
                    "拖尾特效编辑功能",
                    "Trail 编辑器中基础字段、宽度/透明度/时长轨道和预览行为说明。",
                    """
                    适用资源

                    - Trail 编辑器用于 .trail 和 .trail.compiled 拖尾/轨迹资源。
                    - 它通过点列表和参数轨道生成类似剑光、路径残影、运动尾迹的效果。

                    主界面

                    - 中央预览视口：用示例路径显示当前拖尾效果。
                    - 属性面板：编辑资源 ID、基础定义、参数轨道、图像引用和错误信息。

                    基本编辑流程

                    1. 创建或导入 Trail 资源。
                    2. 设置“图像 ID”，确保对应贴图存在。
                    3. 调整“最大点数”和“最小距离”，确定轨迹采样密度和长度。
                    4. 设置“循环”。
                    5. 编辑 WidthOverLength / WidthOverTime 控制宽度。
                    6. 编辑 AlphaOverLength / AlphaOverTime 控制透明度。
                    7. 编辑 TrailDuration 控制拖尾持续时间。
                    8. 在预览里检查形状、长度、透明度和消失速度。
                    9. 保存当前文件，或导出 .trail / .trail.compiled。

                    基础字段

                    - ID：资源 ID。脚本或其他系统引用该拖尾时使用。
                    - 图像 ID：拖尾使用的图像资源 ID。缺失时拖尾可能显示为空白或占位效果。
                    - 最大点数：轨迹最多保留的点数，范围 2 到系统最大值。数值越高，拖尾越长或越平滑，但成本也更高。
                    - 最小距离：新增轨迹点之间的最小距离。值越大，点越稀疏，能减少抖动；值太大可能导致轨迹断裂。
                    - 循环：开启后拖尾按循环方式播放或维持，关闭后会按持续时间逐渐结束。

                    参数轨道通用字段

                    - 时间：节点时间百分比，范围 0 到 100。
                    - 低值：该节点的最小值。
                    - 高值：该节点的最大值。
                    - 曲线：节点之间的变化曲线。
                    - 分布：低值到高值之间的取值方式。
                    - 重置：恢复默认节点。
                    - 添加：新增节点。
                    - 复制：复制当前节点。
                    - 移除：删除节点；每条轨道至少保留一个节点。

                    Trail 轨道

                    - WidthOverLength：沿拖尾长度方向的宽度变化。常用于做“头部粗、尾部细”或反向渐变。
                    - WidthOverTime：随时间变化的整体宽度。可让拖尾生成后逐渐变窄或变宽。
                    - AlphaOverLength：沿拖尾长度方向的透明度变化。常用于让尾端淡出。
                    - AlphaOverTime：随时间变化的整体透明度。可让拖尾在生命周期内淡入或淡出。
                    - TrailDuration：拖尾持续时间。时间越短，拖尾越快消失；时间越长，残影保留越久。

                    调整建议

                    - 拖尾太短：增加最大点数或 TrailDuration。
                    - 拖尾太抖：提高最小距离，或让 WidthOverLength 尾端更细。
                    - 拖尾太亮：降低 AlphaOverLength、AlphaOverTime 或贴图亮度。
                    - 拖尾没有纹理：检查图像 ID 和图像引用的缺失列表。

                    图像引用与错误

                    - 引用：Trail 定义里使用的图像 ID。
                    - 已解析：项目中找到的图像。
                    - 缺失：定义中引用但项目不存在的图像。
                    - 错误：显示轨道编码、文件解析或加载失败信息。
                    """),
                new HelpSectionViewModel(
                    "Showcase 功能",
                    "使用 Lua 把图像、动画、粒子、拖尾和绘图命令组合成可预览场景。",
                    """
                    Showcase 是什么

                    - Showcase 是 Lua 展示脚本编辑器，用来把多个资源组合到同一个预览场景。
                    - 它适合做特效合集预览、技能效果排版、资源联调、导出最终演示动画。
                    - 项目中的 Showcase 文件扩展名是 .lua，位于 scripts 目录。

                    主界面

                    - 中央预览视口：显示脚本运行后的场景。
                    - 脚本面板：编辑 Lua 代码。
                    - 运行：执行当前脚本并刷新场景。
                    - 日志列表：显示 scene.log 输出、运行状态和错误；带行列号的日志可点击定位。

                    基本流程

                    1. 使用“资源 -> 新建资源...”创建 ShowCase，或打开项目中的 .lua。
                    2. 在脚本中调用 scene.clear() 清空场景。
                    3. 使用 scene.reanim(id, x, y)、scene.particle_system(id, x, y)、scene.trail(id, x, y) 创建资源对象。
                    4. 创建 context，并实现 update(dt) 和 draw(g)。
                    5. 使用 scene.regist(context) 注册上下文。
                    6. 点击“运行”，观察预览和日志。
                    7. 修复资源 ID、脚本错误或坐标问题。
                    8. 保存 .lua；需要交付画面时使用“导出预览”导出 PNG、PNG 序列、GIF 或 WebP。

                    常用 scene 能力

                    - scene.clear()：清空当前脚本场景。
                    - scene.log(text)：向日志面板输出文本。
                    - scene.reanim(id, x, y)：创建动画对象。
                    - scene.particle_system(id, x, y)：创建粒子系统对象。
                    - scene.trail(id, x, y)：创建拖尾对象。
                    - scene.resource_exist(id, type)：检查资源是否存在，type 可为 reanim、particle、trail、image、font。
                    - scene.regist(context)：注册带 update/draw 的上下文对象。

                    最小脚本

                    scene.clear()
                    scene.log("showcase initialized")

                    资源组合示例

                    scene.clear()

                    local body = scene.reanim("sample_reanim", 400, 300)
                    local fire = scene.particle_system("fire_burst", 420, 280)
                    local slash = scene.trail("sword_slash", 0, 0)

                    local context = {}
                    local t = 0

                    function context:update(dt)
                        t = t + dt
                        body:set_position(400 + math.sin(t * 2) * 40, 300)
                        slash:clear_points()
                        slash:add_point(330, 320)
                        slash:add_point(470, 280 + math.sin(t * 4) * 40)
                    end

                    function context:draw(g)
                        body:draw(g)
                        fire:draw(g)
                        slash:draw(g)
                    end

                    scene.regist(context)
                    scene.log("showcase initialized")

                    脚本编辑辅助

                    - 支持行号、Lua 语法着色、搜索、当前行高亮、撤销/重做。
                    - 输入 . 或 : 后会尝试显示成员补全。
                    - 常见变量如 scene、global_attachment、graphics、reanim、particle、trail 会参与补全推断。

                    运行与日志

                    - 每次运行会清空上一次日志和场景对象。
                    - 成功运行后，预览视口会绑定脚本创建的帧提供器。
                    - 失败时，预览会停止并选中第一条带位置的错误日志。
                    - 如果日志包含行列号，点击日志会跳转到脚本对应位置。

                    预览导出注意事项

                    - Showcase 导出会在独立运行世界中重新运行脚本，然后从初始状态捕获帧。
                    - 如果脚本依赖随机数、外部状态或运行一段时间后的状态，导出结果可能与当前已经播放过的画面不同。
                    - 导出前建议先点击“运行”，确认日志没有错误。
                    """)
            ];
        }

        private static IReadOnlyList<HelpSectionViewModel> CreateEnglishSections()
        {
            return
            [
                new HelpSectionViewModel(
                    "Project Workflow",
                    "The full workflow from project setup and resource import to editing, previewing, saving, and export.",
                    """
                    Core concepts

                    - Project: the resource container used by EffectViewer. The manifest is project.effectproj.json, and resources usually live under assets/images, assets/fonts, assets/reanims, assets/particles, assets/trails, and scripts.
                    - Resource ID: the name used when animations, particles, trails, and scripts reference each other. Renaming an ID does not automatically update old references in other resources or Lua scripts.
                    - Project path: the relative path stored in the manifest, such as assets/particles/fire.xml or scripts/demo.lua.
                    - Tab asterisk: a * after a tab title means that editor has unsaved changes.

                    Common workflow

                    1. Create, open, or import a project: use Project -> New Project..., Project -> Open Project..., or Project -> Import Project Zip.... Once a current project exists, use Resource -> Import Resource Folder... or Resource -> Import Resource Pak... to append assets.
                    2. Manage resources: expand Images, Fonts, Reanim, Particles, Trails, or Showcases in the Project Explorer. Use the search box to filter by ID, path, or type.
                    3. Open a resource editor: click a resource to open the matching editor. Images and fonts help you inspect source assets; reanim, particle, trail, and Showcase editors handle effect behavior.
                    4. Verify in preview: most editors have a central preview viewport. Use the mouse wheel to zoom, middle-button drag to pan, and double-click to reset the view.
                    5. Save changes: use File -> Save Current File, File -> Save All Files, or Project -> Save Project. Reanim, particle, trail, and Showcase resources write back to their files; image, font, and some metadata are stored in the project manifest.
                    6. Export results: use File -> Export Current File... for the current resource, File -> Export Preview... for stills or animations, and Project -> Export Project Zip... for the whole project.
                    7. Compose a Showcase: when several resources need to appear in one scene, create or open a Showcase, instantiate reanim, particle, and trail resources from Lua, then run the script.

                    Recommended habits

                    - Import images before editing reanim, particle, or trail resources that reference those images.
                    - After changing a resource ID, check image references, script references, and the Project Explorer names.
                    - Save frequently when editing complex animations or particles. Exporting a project Zip is useful as a milestone backup.
                    - Before delivery, export a PNG, GIF, or WebP preview to check bounds, alpha, and timing.
                    """),
                new HelpSectionViewModel(
                    "Import, Export, and Formats",
                    "Project import/export, resource import/export, resource folders, Pak files, and preview export formats.",
                    """
                    Import Project Zip

                    - Entry: Project -> Import Project Zip...
                    - Purpose: import a complete EffectViewer project package, or any Zip that contains project.effectproj.json.
                    - Rules: the Zip must contain a project manifest. The manifest can be at the root or inside a subfolder. Import copies the project into the app-private project folder and creates a unique folder name.
                    - Safety: Zip entries must not use absolute paths or .. path traversal.

                    Export Project Zip

                    - Entry: Project -> Export Project Zip...
                    - Purpose: package the current internal project for backup, migration, or sharing.
                    - Behavior: the app saves the manifest, tries to save open dirty editors, then compresses the files inside the project directory.

                    Import Resource File

                    - Entry: Resource -> Import Resource File...
                    - Purpose: copy one external file into the current project and add it to the manifest.
                    - Supported formats:
                      Image: .png, .jpg, .jpeg, .bmp, .gif, .webp, .tga
                      Font: .ttf
                      Reanim: .reanim, .reanim.compiled
                      Particle: .xml, .xml.compiled
                      Trail: .trail, .trail.compiled
                      ShowCase: .lua
                    - Import rules: image IDs usually start with IMAGE_; other resource IDs default to the sanitized file name. Conflicting IDs or paths get a numeric suffix. The imported resource opens after import.

                    Import Resource Folder

                    - Entry: Resource -> Import Resource Folder...
                    - Purpose: append an external resource directory to the current project and add imported assets to the manifest.
                    - Conflict handling: if an imported resource ID already exists, choose Skip, Overwrite, or Keep Both. The same choice can be applied to remaining conflicts.
                    - Recommended structure:
                      properties/resources.xml
                      reanim/*.reanim
                      particles/*.xml
                      particles/*.trail
                    - Compiled resources can also be imported:
                      compiled/reanim/*.reanim.compiled
                      compiled/particles/*.xml.compiled
                      compiled/particles/*.trail.compiled
                      compiled/trails/*.trail.compiled
                    - resources.xml is used for Image, Font, rows, cols, SetDefaults path, and idprefix metadata.
                    - Alpha companion images can be named _name.png or name_.png. A standalone companion image is treated as alphaOnly.

                    Import Resource Pak

                    - Entry: Resource -> Import Resource Pak...
                    - Purpose: read a .pak as a resource folder and append it to the current project.
                    - Behavior: this follows the resource-folder import flow and uses the same Skip, Overwrite, or Keep Both choices for duplicate IDs.

                    Drag and drop import

                    - You can drop a supported single resource file or a .pak onto the Project Explorer.
                    - Single resource files and .pak files require a writable current project. A .pak follows Pak import and appends to the current project.
                    - Resource folders and project Zip files are not supported through drag and drop.

                    Export Current File

                    - Entry: File -> Export Current File...
                    - Purpose: export the file behind the current editor to a location outside the project.
                    - Image, Font, and ShowCase: export the original project file.
                    - Reanim: export .reanim or .reanim.compiled.
                    - Particle: export .xml or .xml.compiled.
                    - Trail: export .trail or .trail.compiled.
                    - If the editor has unsaved changes, the app saves it before exporting.

                    Export Preview

                    - Entry: File -> Export Preview...
                    - Supported editors: Image, Font, Reanim, Particle, Trail, ShowCase.
                    - Formats:
                      Png: one PNG image, useful for static checks.
                      PngSequenceZip: a Zip containing frame-by-frame PNGs, useful for compositing or frame inspection.
                      Gif: broadly compatible and convenient for quick sharing.
                      Webp: better compression, useful for web output.
                    - Common options:
                      Format: output type.
                      Canvas Scale: output resolution multiplier.
                      FPS: frame rate for animation export.
                      Duration: capture duration when the editor has no explicit timeline.
                    - Reanim can export the full timeline or recognized layer ranges, with Start Frame and End Frame.
                    - Particle and Trail use Start Second and End Second, with an option to export until provider completion.
                    - ShowCase export reruns the script in a separate runtime world, then captures from the initial state.
                    """),
                new HelpSectionViewModel(
                    "Animation Editing",
                    "A detailed guide to the Reanim editor, editable areas, and the basic animation creation flow.",
                    """
                    Supported resources

                    - The Reanim editor handles .reanim and .reanim.compiled animation definitions.
                    - It is designed for editing tracks, frames, transforms, image bindings, text frames, and tween metadata.

                    Main layout

                    - Preview viewport: displays the current animation. Use the mouse wheel to zoom, middle-button drag to pan, and double-click to reset.
                    - Properties panel: edits the resource ID, structure info, selected-frame transform, tweens, image references, and errors.
                    - Timeline: displays tracks and frames, lets you select frames, play the animation, and toggle track visibility.

                    Basic creation flow

                    1. Create a Reanim from Resource -> New Resource..., then enter a resource ID.
                    2. Use Animation -> Add Track to add the layers you need, such as body, arm, or fx_fire.
                    3. In the Transform section, set Track Name, ImageID, Font, or Text.
                    4. Set X/Y, ScaleX/ScaleY, SkewX/SkewY, Frame, Alpha, and Visible for the selected frame.
                    5. Use Animation -> Add Frame to extend the timeline, then edit object positions or image frames on different frames.
                    6. For continuous motion, create tween metadata with Add, or edit key frames and click Detect to infer tweens.
                    7. Set FPS in the timeline and enable Play to check timing.
                    8. Check ImageReferences -> Missing, then import or correct the required ImageID values.
                    9. Save the current file. Export source or compiled formats with File -> Export Current File....

                    Timeline controls

                    - Layer selector: switches between the full timeline and recognized animation layer or track ranges. Preview export can use these ranges too.
                    - FPS: playback frames per second. It affects editor playback speed and the default preview export frame rate.
                    - Play: starts or pauses timeline playback.
                    - Free Transform: lets you drag the selected frame object in the viewport. Drag inside the object to move, square handles to scale, and diamond handles to skew.

                    Track and frame actions

                    - Add Track: appends a new track. The new track matches the current frame count.
                    - Remove Track: removes the selected track. At least one track must remain.
                    - Add Frame: inserts a frame after the selected frame across all tracks.
                    - Remove Frame: removes the selected frame across all tracks. At least one frame must remain.
                    - Track visibility: toggles whether a track participates in preview.

                    Selected-frame transform fields

                    - ID: resource ID used by scripts and other resources.
                    - Track Name: current track name. Clear names make tweens, layer selection, and script track lookup easier.
                    - ImageID: image resource ID bound to the selected frame. Empty frames may draw nothing.
                    - Font: font resource ID used by the selected frame.
                    - Text: text content drawn by the selected frame.
                    - X / Y: translation position.
                    - ScaleX / ScaleY: horizontal and vertical scale.
                    - SkewX / SkewY: rotation/skew-related transform parameters.
                    - Frame: spritesheet frame index. For multi-row or multi-column images, this selects the cell. -1 usually means invisible.
                    - Alpha: opacity, from 0 to 1.
                    - Visible: controls whether the selected frame is visible. Turning it off stores an invisible-frame state.

                    Transform editing limits

                    - Direct transform editing is disabled when the selected frame is inside a tween range.
                    - To edit a frame inside a tween, make it a Keyframe first, or Bake/Remove the related tween.

                    Tween features

                    - Detect: infers tween metadata from current frame data.
                    - Add: creates a tween near the selected track and frame.
                    - Bake All: writes all tween results into frame data and clears tween metadata.
                    - Track: tween track number, shown from 1.
                    - Track Name: read-only name of the tween track.
                    - Start Frame: first frame of the tween, shown from 1.
                    - End Frame: last frame of the tween, shown from 1.
                    - Anchor X / Anchor Y: anchor point used for tween calculation.
                    - Keyframe: converts the selected in-between tween frame into a directly editable key frame.
                    - Bake: writes the selected tween into frame data and removes that tween.
                    - Remove: deletes the selected tween metadata.

                    Image references and errors

                    - References: image IDs requested by the animation file.
                    - Resolved: image resources found in the project.
                    - Missing: image IDs referenced by the animation but missing from the project.
                    - Errors: parse, load, or encode issues.
                    """),
                new HelpSectionViewModel(
                    "Particle Effect Editing",
                    "A detailed guide to particle emitters, flags, parameter tracks, and field settings.",
                    """
                    Supported resources

                    - The Particle editor handles .xml and .xml.compiled particle systems.
                    - A particle system can contain multiple emitters. Each emitter has its own image, lifetime, spawn, color, transform, rendering, and field settings.

                    Main layout

                    - Preview viewport: continuously plays the current particle system.
                    - Properties panel: edits resource ID, emitters, emitter fields, parameter tracks, ParticleFields, SystemFields, image references, and errors.

                    Basic editing flow

                    1. Create or import a Particle resource.
                    2. In Definition, choose an emitter or click Add to create one.
                    3. In General, set Name, ImageID, ImageRow, ImageCol, ImageFrames, and EmitterType.
                    4. Enable Flags as needed to control looping, blending, and following behavior.
                    5. Tune tracks under Lifetime, Spawn, EmitterShape, Launch, SystemColor, ParticleColor, ParticleTransform, Rendering, and Collision.
                    6. For force fields or extra motion, add ParticleFields or SystemFields, then set FieldType, x, and y tracks.
                    7. Check the preview and ImageReferences -> Missing.
                    8. Save the current file, or export .xml / .xml.compiled.

                    Emitter list

                    - Emitter selector: chooses the emitter currently being edited.
                    - Add: appends a new emitter.
                    - Remove: deletes the current emitter. Keeping at least one emitter is usually safer.
                    - Display name: shows "index. Name" when Name exists, otherwise a fallback emitter name.

                    General fields

                    - Name: emitter name, useful for distinguishing multiple emitters.
                    - ImageID: image resource ID used by particles. If empty or missing, particles often have no visible texture.
                    - ImageRow: spritesheet row index, starting at 0.
                    - ImageCol: spritesheet column index, starting at 0.
                    - ImageFrames: number of image frames available for particle animation. Minimum is 1.
                    - EmitterType: controls emitter shape or emission behavior.
                    - Animated: plays image frames as an animation. AnimationRate controls playback speed.

                    Flags

                    - RandomLaunchSpin: gives particles a random initial spin at launch.
                    - AlignLaunchSpin: aligns particle rotation to launch direction.
                    - AlignToPixels: snaps particle positions to pixels to reduce subpixel jitter.
                    - SystemLoops: loops the particle system after its system duration.
                    - ParticleLoops: loops individual particle animation or lifetime behavior.
                    - ParticlesDontFollow: particles stop following the emitter after they spawn.
                    - DieIfOverloaded: allows the system to die when particle count or load exceeds limits.
                    - Additive: uses additive blending, useful for fire, glow, and energy effects.
                    - Fullscreen: handles related rendering as fullscreen or screen-space behavior.
                    - SoftwareOnly: used only on software rendering paths.
                    - HardwareOnly: used only on hardware rendering paths.

                    Parameter track node fields

                    - Time: node time percentage, from 0 to 100. Nodes are sorted by time.
                    - Low: minimum value at this node.
                    - High: maximum value at this node. When Low and High differ, the runtime can choose or distribute a value between them.
                    - Curve: value curve from this node toward the next node.
                    - Distribution: distribution between Low and High.
                    - Reset: restores the default single-node track.
                    - Add: adds a track node.
                    - Copy: duplicates the current node and offsets time slightly forward.
                    - Remove: deletes the current node. Each track keeps at least one node.

                    Lifetime tracks

                    - SystemDuration: duration of the whole particle system.
                    - CrossFadeDuration: fade duration when crossing between emitters or states.
                    - ParticleDuration: lifetime of each particle.

                    Spawn tracks

                    - SpawnRate: rate at which particles are spawned.
                    - SpawnMinActive: minimum active particles to maintain; -1 usually means no forced minimum.
                    - SpawnMaxActive: maximum active particles. Low values limit density.
                    - SpawnMaxLaunched: maximum total launched particles. Spawning stops after this is reached.

                    EmitterShape tracks

                    - EmitterRadius: circular or radius-related spawn area.
                    - EmitterOffsetX / EmitterOffsetY: emitter offset from its origin.
                    - EmitterBoxX / EmitterBoxY: width/height or box range for rectangular emission.
                    - EmitterSkewX / EmitterSkewY: skew of the emitter area.
                    - EmitterPath: path-related emission control.

                    Launch tracks

                    - LaunchSpeed: initial particle speed.
                    - LaunchAngle: initial launch angle.

                    SystemColor tracks

                    - SystemRed / SystemGreen / SystemBlue: system-level RGB multipliers.
                    - SystemAlpha: system-level opacity multiplier.
                    - SystemBrightness: system-level brightness multiplier.

                    ParticleColor tracks

                    - ParticleRed / ParticleGreen / ParticleBlue: per-particle RGB multipliers.
                    - ParticleAlpha: per-particle opacity.
                    - ParticleBrightness: per-particle brightness.

                    ParticleTransform tracks

                    - ParticleScale: particle scale.
                    - ParticleStretch: particle stretch ratio.
                    - ParticleSpinAngle: initial or current particle rotation angle.
                    - ParticleSpinSpeed: particle rotation speed.

                    Rendering tracks

                    - AnimationRate: image-frame animation speed.
                    - ClipTop / ClipBottom / ClipLeft / ClipRight: clips particle drawing from each side.

                    Collision tracks

                    - CollisionReflect: bounce strength after collision.
                    - CollisionSpin: extra spin after collision.

                    ParticleFields and SystemFields

                    - ParticleFields: usually act on individual particles, changing velocity, position, acceleration, or other motion parameters.
                    - SystemFields: usually act on the system or emitter context, affecting overall motion or spawn environment.
                    - FieldType: selects what the field does.
                    - x: X direction or first parameter track.
                    - y: Y direction or second parameter track.
                    - Add: adds a field.
                    - Remove: removes the current field.

                    Image references and errors

                    - References: image IDs used by the particle definition.
                    - Resolved: images found and loadable in the project.
                    - Missing: images referenced by the particle but missing from the project.
                    - If the preview shows no particles, first check ImageID, SpawnRate, ParticleDuration, ParticleAlpha, ImageRow, ImageCol, and ImageFrames.
                    """),
                new HelpSectionViewModel(
                    "Trail Effect Editing",
                    "A detailed guide to trail base fields, width/alpha/duration tracks, and preview behavior.",
                    """
                    Supported resources

                    - The Trail editor handles .trail and .trail.compiled resources.
                    - Trails use a point list plus parameter tracks to create path streaks, slashes, motion trails, and similar effects.

                    Main layout

                    - Preview viewport: shows the current trail effect on a sample path.
                    - Properties panel: edits resource ID, base definition, parameter tracks, image references, and errors.

                    Basic editing flow

                    1. Create or import a Trail resource.
                    2. Set ImageID and make sure the texture exists.
                    3. Adjust MaxPoints and MinDist to control sampling density and trail length.
                    4. Set Loops.
                    5. Edit WidthOverLength / WidthOverTime to control width.
                    6. Edit AlphaOverLength / AlphaOverTime to control opacity.
                    7. Edit TrailDuration to control how long the trail lasts.
                    8. Check shape, length, opacity, and fade speed in preview.
                    9. Save the current file, or export .trail / .trail.compiled.

                    Base fields

                    - ID: resource ID used by scripts or other systems.
                    - ImageID: image resource ID used by the trail. If missing, the trail may render blank or as a placeholder.
                    - MaxPoints: maximum number of retained trail points, from 2 up to the system limit. Higher values can make the trail longer or smoother, with higher cost.
                    - MinDist: minimum distance between new trail points. Higher values make points sparser and can reduce jitter; too high can break the path.
                    - Loops: enables looped trail behavior. When disabled, the trail ends according to duration.

                    Parameter track node fields

                    - Time: node time percentage, from 0 to 100.
                    - Low: minimum value at this node.
                    - High: maximum value at this node.
                    - Curve: value curve between nodes.
                    - Distribution: distribution between Low and High.
                    - Reset: restores the default node.
                    - Add: adds a node.
                    - Copy: duplicates the current node.
                    - Remove: deletes a node. Each track keeps at least one node.

                    Trail tracks

                    - WidthOverLength: width change along the trail length. Useful for a wide head and narrow tail, or the reverse.
                    - WidthOverTime: overall width change over time. This can make the trail shrink or grow after it spawns.
                    - AlphaOverLength: opacity change along the trail length. Often used to fade the tail.
                    - AlphaOverTime: overall opacity change over time. This can fade the trail in or out during its lifetime.
                    - TrailDuration: trail lifetime. Shorter values fade quickly; longer values keep the afterimage longer.

                    Tuning tips

                    - Trail too short: increase MaxPoints or TrailDuration.
                    - Trail jittery: increase MinDist, or make the tail thinner in WidthOverLength.
                    - Trail too bright: reduce AlphaOverLength, AlphaOverTime, or texture brightness.
                    - No texture: check ImageID and ImageReferences -> Missing.

                    Image references and errors

                    - References: image IDs used by the Trail definition.
                    - Resolved: images found in the project.
                    - Missing: image IDs referenced by the definition but missing from the project.
                    - Errors: track encoding, file parsing, or load failures.
                    """),
                new HelpSectionViewModel(
                    "Showcase",
                    "Use Lua to compose images, animations, particles, trails, and drawing commands in one preview scene.",
                    """
                    What Showcase does

                    - Showcase is the Lua script editor for composing multiple resources in one preview scene.
                    - It is useful for effect collections, skill-effect layouts, resource integration checks, and final demo animation exports.
                    - Project Showcase files use the .lua extension and live under scripts.

                    Main layout

                    - Preview viewport: displays the scene after the script runs.
                    - Script panel: edits Lua code.
                    - Run: executes the current script and refreshes the scene.
                    - Logs: shows scene.log output, run status, and errors. Logs with line/column info can navigate to the script location.

                    Basic flow

                    1. Create a ShowCase from Resource -> New Resource..., or open a project .lua file.
                    2. Call scene.clear() in the script to clear the scene.
                    3. Use scene.reanim(id, x, y), scene.particle_system(id, x, y), and scene.trail(id, x, y) to create resource objects.
                    4. Create a context object and implement update(dt) and draw(g).
                    5. Register it with scene.regist(context).
                    6. Click Run, then inspect the preview and logs.
                    7. Fix resource IDs, script errors, or coordinates.
                    8. Save the .lua file. Use Export Preview for PNG, PNG sequence, GIF, or WebP output.

                    Common scene APIs

                    - scene.clear(): clears the current script scene.
                    - scene.log(text): writes text to the log panel.
                    - scene.reanim(id, x, y): creates a reanimation object.
                    - scene.particle_system(id, x, y): creates a particle system object.
                    - scene.trail(id, x, y): creates a trail object.
                    - scene.resource_exist(id, type): checks whether a resource exists. type can be reanim, particle, trail, image, or font.
                    - scene.regist(context): registers an object that has update/draw callbacks.

                    Minimal script

                    scene.clear()
                    scene.log("showcase initialized")

                    Resource composition example

                    scene.clear()

                    local body = scene.reanim("sample_reanim", 400, 300)
                    local fire = scene.particle_system("fire_burst", 420, 280)
                    local slash = scene.trail("sword_slash", 0, 0)

                    local context = {}
                    local t = 0

                    function context:update(dt)
                        t = t + dt
                        body:set_position(400 + math.sin(t * 2) * 40, 300)
                        slash:clear_points()
                        slash:add_point(330, 320)
                        slash:add_point(470, 280 + math.sin(t * 4) * 40)
                    end

                    function context:draw(g)
                        body:draw(g)
                        fire:draw(g)
                        slash:draw(g)
                    end

                    scene.regist(context)
                    scene.log("showcase initialized")

                    Script editing helpers

                    - The editor supports line numbers, Lua syntax highlighting, search, current-line highlight, undo, and redo.
                    - Typing . or : attempts member completion.
                    - Common variables such as scene, global_attachment, graphics, reanim, particle, and trail participate in completion inference.

                    Running and logs

                    - Each run clears the previous logs and scene objects.
                    - On success, the preview viewport binds to the frame provider created by the script.
                    - On failure, preview stops and the first positioned error log is selected.
                    - If a log contains line and column information, clicking it jumps to that script location.

                    Preview export notes

                    - Showcase export reruns the script in a separate runtime world, then captures frames from the initial state.
                    - If the script depends on randomness, external state, or a scene that has already played for a while, export may differ from the current preview state.
                    - Before exporting, click Run and make sure the logs have no errors.
                    """)
            ];
        }
    }
}
