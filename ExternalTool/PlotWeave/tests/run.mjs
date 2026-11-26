/**
 * 顺序载入全部测试模块。
 *
 * 这样测试不依赖 `node --test` 的子进程隔离，也能在受限环境（如 Windows
 * 沙箱、CI 的单进程模式）里运行。任一测试失败时 node:test 会把进程退出码
 * 置为 1。
 */
import './core.test.mjs';
import './project-serialization.test.mjs';
import './generator-map.test.mjs';
import './extensions.test.mjs';
import './ui-fields.test.mjs';
import './groups.test.mjs';
import './polyomino.test.mjs';
import './strip-templates.test.mjs';
import './floor-generator.test.mjs';
import './floor-edges.test.mjs';
import './floor-exploration.test.mjs';
import './map-pool-exploration-api.test.mjs';
import './floor-contacts.test.mjs';
